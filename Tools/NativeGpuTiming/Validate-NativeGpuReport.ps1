function Test-DamageNativeGpuReport($Report, [string]$CalibrationEvidencePath) {
    $errors=[Collections.Generic.List[string]]::new()
    try {
        if(-not $CalibrationEvidencePath){throw 'Native GPU reports require calibration evidence.'}
        $calibration=Get-Content -LiteralPath $CalibrationEvidencePath -Raw | ConvertFrom-Json
        $calibrationBuild=Get-Content -LiteralPath (Join-Path (Split-Path -Parent $CalibrationEvidencePath) '../build/DamageTextComparison.apk.build.json') -Raw | ConvertFrom-Json
        if((Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $CalibrationEvidencePath) $calibration.archive)).Hash -ne $calibration.archiveSHA256){throw 'Calibration archive hash mismatch.'}
        if($calibration.schema -ne 'damage-native-gpu-calibration-device-evidence-v1' -or @($calibration.runs).Count -ne 3 -or
            @($calibration.runs | Where-Object {-not $_.candidate -or -not $_.complete -or $_.required -lt 560 -or $_.errors.Count -ne 0}).Count -ne 0){throw 'Calibration evidence did not pass.'}
        $trace=$Report.nativeGpu
        if($trace.schema -ne 'damage-native-gpu-capture-v1' -or -not $trace.complete -or
            $trace.calibrationSHA256 -ne (Get-FileHash -LiteralPath $CalibrationEvidencePath).Hash -or
            $trace.pluginSHA256 -ne $calibrationBuild.nativePluginSHA256 -or $Report.gpuScope -ne 'HUD-camera-instrumented' -or
            $Report.executionMode -ne 'Player' -or $Report.graphicsApi -ne 'Vulkan' -or $Report.backend -ne 'IL2CPP' -or $Report.buildType -ne 'Release'){
            throw 'Native trace identity, scope, build, or completion mismatch.'
        }
        $info=$trace.info
        foreach($key in @('timestampPeriodNs','timestampValidBits','queueFamily','vendorId','deviceId','apiVersion','driverVersion')){
            foreach($run in $calibration.runs){if($info.$key -ne $run.info.$key){$errors.Add('native-device-capability-changed:'+$key)}}
        }
        if($info.status -ne 1 -or $info.capacity -ne $trace.capacity -or $trace.commandBuffers -ne $trace.capacity*2 -or
            $trace.drainFrames -ne 16 -or $trace.capacity -gt 8192 -or $trace.setupCpuMs -le 0 -or
            $info.timestampPeriodNs -le 0 -or $info.timestampValidBits -lt 1 -or $info.timestampValidBits -gt 64){throw 'Native capability/preallocation/drain metadata invalid.'}
        if(@($trace.cameraFrames).Count -ne $trace.capturedFrames -or @($trace.timings).Count -ne $trace.capturedFrames -or
            $trace.capturedFrames -ne @($Report.frames).Count+16 -or $trace.capturedFrames -gt $trace.capacity){throw 'Native actual-frame coverage incomplete.'}
        $modulus=[decimal]1; for($bit=0;$bit -lt $info.timestampValidBits;$bit++){$modulus*=2}
        for($i=0;$i -lt $trace.capturedFrames;$i++){
            $camera=$trace.cameraFrames[$i]
            if($camera.unityFrame -ne $trace.firstUnityFrame+$i -or $camera.cameraPasses -ne 1 -or
                $camera.preCullFrame -ne $camera.unityFrame -or $camera.postRenderFrame -ne $camera.unityFrame -or $camera.bindingCpuMs -lt 0){$errors.Add('camera-frame-coverage')}
        }
        $matched=0
        foreach($frame in $Report.frames){
            if([int]$frame.stage -ne 3){continue}
            $slot=[int]$frame.unityFrame-[int]$trace.firstUnityFrame
            if($slot -lt 0 -or $slot -ge $trace.capturedFrames){$errors.Add('missing-native-slot');continue}
            $n=$trace.timings[$slot]
            if($n.index -ne $slot -or $n.unityFrame -ne $frame.unityFrame -or $n.beginCalls -ne 1 -or $n.endCalls -ne 1 -or
                $n.beginNativeFrame -ne $n.endNativeFrame -or [decimal]$n.readSafeFrame -lt [decimal]$n.endNativeFrame -or
                [decimal]$n.readNativeFrame -lt [decimal]$n.readSafeFrame){$errors.Add('native-token-or-fence')}
            if($n.available -ne 1 -or $n.status -ne 2 -or -not $frame.gpuAvailable -or $n.milliseconds -le 0 -or
                [double]::IsNaN($n.milliseconds) -or [double]::IsInfinity($n.milliseconds) -or
                [Math]::Abs($frame.gpuMs-$n.milliseconds) -gt .000001){$errors.Add('native-gpu-unavailable-or-mismatch')}
            $begin=[decimal][Convert]::ToUInt64($n.beginTimestamp,16);$end=[decimal][Convert]::ToUInt64($n.endTimestamp,16)
            $expected=[double]((($end-$begin+$modulus)%$modulus)*[decimal]$info.timestampPeriodNs/1000000)
            if($begin -ge $modulus -or $end -ge $modulus -or [Math]::Abs($expected-$n.milliseconds) -gt .000001){$errors.Add('native-timestamp-scale')}
            $matched++
        }
        if($matched -lt 3000 -or $matched -ne $Report.samples -or $matched -ne $trace.matchedSamples){$errors.Add('native-insufficient-samples')}
    }catch{$errors.Add($_.Exception.Message)}
    [pscustomobject]@{valid=$errors.Count -eq 0;errors=@($errors.ToArray())}
}
