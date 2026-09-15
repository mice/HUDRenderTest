## 
支持8 Texture in One dc
支持 缩放
支持 旋转


如何引入JobSystem
1 当我们UIMeshData导出为NativeMeshData,然后放入一个NativeArray.行不通
InvalidOperationException: NativeMeshData used in NativeArray<NativeMeshData> must be unmanaged (contain no managed types) and cannot itself be a native container type.
2 每一个UIMeshData的发一个Job出来,然后一个最终Job依赖所有的Jobs

这里做了一个特殊的处理
把Task 伪装为一个Job.然后在JobSystem中运行

例子:
TestGraphicsDataWithJob
是一个基于JobSystem组件Mesh的测试.


TestGraphicsDataMetric
测试如果不使用job合并网格需要的时间.




