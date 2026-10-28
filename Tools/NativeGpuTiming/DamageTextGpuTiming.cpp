// Sample diagnostic only. Uses Unity's installed PluginAPI headers and Android NDK.
// Each slot is written once. Results are polled after Unity's safe-frame fence;
// no WAIT_BIT, queue submission, interception, or GPU/CPU clock substitution.
#define VK_NO_PROTOTYPES
#include "IUnityGraphics.h"
#include "IUnityGraphicsVulkan.h"
#include <cstdint>
#include <mutex>
#include <vector>

struct TimingInfo
{
    double timestampPeriodNs;
    uint32_t timestampValidBits, queueFamily, vendorId, deviceId, apiVersion, driverVersion, capacity;
    int32_t status;
};
struct TimingSample
{
    uint64_t beginTimestamp, endTimestamp, beginNativeFrame, endNativeFrame;
    uint64_t readSafeFrame, readNativeFrame;
    uint32_t unityFrame, index, beginCalls, endCalls, available;
    int32_t status;
    double milliseconds;
};
static_assert(sizeof(TimingInfo) == 40 && sizeof(TimingSample) == 80, "Managed ABI mismatch");
static IUnityInterfaces* interfaces;
static IUnityGraphics* graphics;
static IUnityGraphicsVulkan* vulkan;
static UnityVulkanInstance instance{};
static VkQueryPool pool = VK_NULL_HANDLE;
static PFN_vkCreateQueryPool createPool;
static PFN_vkDestroyQueryPool destroyPool;
static PFN_vkCmdResetQueryPool resetQueries;
static PFN_vkCmdWriteTimestamp writeTimestamp;
static PFN_vkGetQueryPoolResults getResults;
static PFN_vkDeviceWaitIdle waitIdle;
static TimingInfo info{};
static std::vector<TimingSample> samples;
static uint32_t nextRead;
static int eventBase;
static std::mutex stateMutex;

static void CollectCompleted(uint64_t safeFrame, uint64_t currentFrame)
{
    if (safeFrame > currentFrame) return; // Do not interpret a sentinel as a completed GPU fence.
    while (nextRead < samples.size())
    {
        auto& row = samples[nextRead];
        if (row.endCalls == 0 || row.endNativeFrame > safeFrame) break;
        if (row.status < 0) { ++nextRead; continue; }
        uint64_t values[4]{}; // timestamp, availability for each of the two queries
        VkResult result = getResults(instance.device, pool, nextRead * 2, 2,
            sizeof(values), values, sizeof(uint64_t) * 2,
            VK_QUERY_RESULT_64_BIT | VK_QUERY_RESULT_WITH_AVAILABILITY_BIT);
        if (result == VK_NOT_READY || values[1] == 0 || values[3] == 0) break;
        if (result != VK_SUCCESS) { row.status = -10; ++nextRead; continue; }
        const uint64_t mask = info.timestampValidBits == 64 ? UINT64_MAX : ((uint64_t{1} << info.timestampValidBits) - 1);
        row.beginTimestamp = values[0] & mask; row.endTimestamp = values[2] & mask;
        const uint64_t delta = (row.endTimestamp - row.beginTimestamp) & mask;
        row.milliseconds = static_cast<double>(delta) * info.timestampPeriodNs / 1000000.0;
        row.available = 1; row.status = delta > 0 ? 2 : -11;
        row.readSafeFrame = safeFrame; row.readNativeFrame = currentFrame;
        ++nextRead;
    }
}

static void UNITY_INTERFACE_API RenderEvent(int eventId, void* data)
{
    std::lock_guard<std::mutex> guard(stateMutex);
    if (info.status != 1 || pool == VK_NULL_HANDLE) return;
    const uint64_t token = reinterpret_cast<uintptr_t>(data);
    const uint32_t encodedIndex = static_cast<uint32_t>(token);
    if (encodedIndex == 0 || encodedIndex > samples.size()) { info.status = -8; return; }
    const uint32_t index = encodedIndex - 1, frame = static_cast<uint32_t>(token >> 32);
    UnityVulkanRecordingState recording{};
    if (!vulkan->CommandRecordingState(&recording, kUnityVulkanGraphicsQueueAccess_DontCare)) { info.status = -9; return; }
    CollectCompleted(recording.safeFrameNumber, recording.currentFrameNumber);
    auto& row = samples[index];
    if (eventId == eventBase)
    {
        if (++row.beginCalls != 1) { row.status = -12; return; }
        row.index = index; row.unityFrame = frame; row.beginNativeFrame = recording.currentFrameNumber;
        row.status = 1;
        // ConfigureEvent guarantees an outside-render-pass command buffer.
        resetQueries(recording.commandBuffer, pool, index * 2, 2);
        writeTimestamp(recording.commandBuffer, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, pool, index * 2);
    }
    else if (eventId == eventBase + 1)
    {
        if (++row.endCalls != 1 || row.beginCalls != 1 || row.unityFrame != frame) { row.status = -13; return; }
        row.endNativeFrame = recording.currentFrameNumber;
        writeTimestamp(recording.commandBuffer, VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, pool, index * 2 + 1);
    }
    else info.status = -14;
}

static void UNITY_INTERFACE_API DeviceEvent(UnityGfxDeviceEventType type)
{
    std::lock_guard<std::mutex> guard(stateMutex);
    if (type == kUnityGfxDeviceEventInitialize)
    {
        info = {}; info.status = -1;
        if (graphics->GetRenderer() != kUnityGfxRendererVulkan) return;
        vulkan = interfaces->Get<IUnityGraphicsVulkan>();
        if (!vulkan) { info.status = -2; return; }
        instance = vulkan->Instance();
        auto getDeviceProc = reinterpret_cast<PFN_vkGetDeviceProcAddr>(instance.getInstanceProcAddr(instance.instance, "vkGetDeviceProcAddr"));
        auto getProperties = reinterpret_cast<PFN_vkGetPhysicalDeviceProperties>(instance.getInstanceProcAddr(instance.instance, "vkGetPhysicalDeviceProperties"));
        auto getQueues = reinterpret_cast<PFN_vkGetPhysicalDeviceQueueFamilyProperties>(instance.getInstanceProcAddr(instance.instance, "vkGetPhysicalDeviceQueueFamilyProperties"));
        if (!getDeviceProc || !getProperties || !getQueues) { info.status = -3; return; }
#define LOAD(name, target) target = reinterpret_cast<PFN_##name>(getDeviceProc(instance.device, #name))
        LOAD(vkCreateQueryPool, createPool); LOAD(vkDestroyQueryPool, destroyPool);
        LOAD(vkCmdResetQueryPool, resetQueries); LOAD(vkCmdWriteTimestamp, writeTimestamp);
        LOAD(vkGetQueryPoolResults, getResults); LOAD(vkDeviceWaitIdle, waitIdle);
#undef LOAD
        if (!createPool || !destroyPool || !resetQueries || !writeTimestamp || !getResults || !waitIdle) { info.status = -3; return; }
        VkPhysicalDeviceProperties properties{}; getProperties(instance.physicalDevice, &properties);
        uint32_t count = 0; getQueues(instance.physicalDevice, &count, nullptr);
        std::vector<VkQueueFamilyProperties> queues(count); getQueues(instance.physicalDevice, &count, queues.data());
        if (instance.queueFamilyIndex >= count || queues[instance.queueFamilyIndex].timestampValidBits == 0 || properties.limits.timestampPeriod <= 0) { info.status = -4; return; }
        info.timestampPeriodNs = properties.limits.timestampPeriod;
        info.timestampValidBits = queues[instance.queueFamilyIndex].timestampValidBits;
        info.queueFamily = instance.queueFamilyIndex; info.vendorId = properties.vendorID; info.deviceId = properties.deviceID;
        info.apiVersion = properties.apiVersion; info.driverVersion = properties.driverVersion; info.status = 0;
        eventBase = graphics->ReserveEventIDRange(2);
        UnityVulkanPluginEventConfig config{};
        config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
        config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
        config.flags = 0; // Do not flush, submit, or serialize worker threads.
        vulkan->ConfigureEvent(eventBase, &config); vulkan->ConfigureEvent(eventBase + 1, &config);
    }
    else if (type == kUnityGfxDeviceEventShutdown)
    {
        // No waits occur during sampling. Pool resources live until device shutdown.
        if (pool != VK_NULL_HANDLE) { waitIdle(instance.device); destroyPool(instance.device, pool, nullptr); pool = VK_NULL_HANDLE; }
        samples.clear(); info.status = -15; vulkan = nullptr; instance = {};
    }
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* value)
{
    interfaces = value; graphics = interfaces->Get<IUnityGraphics>();
    graphics->RegisterDeviceEventCallback(DeviceEvent); DeviceEvent(kUnityGfxDeviceEventInitialize);
}
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    graphics->UnregisterDeviceEventCallback(DeviceEvent);
    DeviceEvent(kUnityGfxDeviceEventShutdown);
}
extern "C" int UNITY_INTERFACE_EXPORT DamageGpuConfigure(uint32_t capacity)
{
    std::lock_guard<std::mutex> guard(stateMutex);
    if (info.status < 0) return info.status;
    if (pool != VK_NULL_HANDLE) return -6; // Never reset/reuse slots while commands may be in flight.
    if (capacity == 0 || capacity > 100000) return -7;
    VkQueryPoolCreateInfo create{}; create.sType = VK_STRUCTURE_TYPE_QUERY_POOL_CREATE_INFO;
    create.queryType = VK_QUERY_TYPE_TIMESTAMP; create.queryCount = capacity * 2;
    if (createPool(instance.device, &create, nullptr, &pool) != VK_SUCCESS) { info.status = -5; return info.status; }
    samples.resize(capacity); nextRead = 0; info.capacity = capacity; info.status = 1;
    return 1;
}
extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT DamageGpuGetEvent() { return RenderEvent; }
extern "C" int UNITY_INTERFACE_EXPORT DamageGpuGetEventBase() { return eventBase; }
extern "C" void UNITY_INTERFACE_EXPORT DamageGpuGetInfo(TimingInfo* output)
{
    std::lock_guard<std::mutex> guard(stateMutex); *output = info;
}
extern "C" int UNITY_INTERFACE_EXPORT DamageGpuGetSamples(TimingSample* output, uint32_t capacity)
{
    std::lock_guard<std::mutex> guard(stateMutex);
    if (capacity < samples.size()) return -1;
    for (size_t i = 0; i < samples.size(); ++i) output[i] = samples[i];
    return static_cast<int>(samples.size());
}
