[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

$network = @{
    icon = [string][char]0xE871
    summary = 'Offline'
}

try {
    $connectivity = [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]
    $profile = $connectivity::GetInternetConnectionProfile()
    if ($null -eq $profile) {
        $profile = $connectivity::GetConnectionProfiles() |
            Where-Object { $_.GetNetworkConnectivityLevel() -ne 'None' } |
            Select-Object -First 1
    }

    if ($null -ne $profile) {
        $level = $profile.GetNetworkConnectivityLevel()
        if ($level -ne 'None') {
            if ($profile.IsWlanConnectionProfile) {
                $network.icon = [string][char]0xE701
                $kind = 'Wi-Fi'
            } elseif ($profile.NetworkAdapter.IanaInterfaceType -eq 6) {
                $network.icon = [string][char]0xE839
                $kind = 'Ethernet'
            } else {
                $network.icon = [string][char]0xE774
                $kind = 'Network'
            }

            $network.summary = if ($level -eq 'InternetAccess') { "$kind connected" } else { "$kind, no internet" }
        }
    }
} catch {
    $network.summary = 'Network status unavailable'
}

$network | ConvertTo-Json -Compress
