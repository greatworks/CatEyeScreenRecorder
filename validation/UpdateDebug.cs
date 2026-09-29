using System;
using FreeWindowsScreenRecorder;
class DebugMain { static void Main() { string json = "{\"tag_name\":\"v9.9.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/example/cateye/releases/tag/v9.9.0\",\"assets\":[{\"name\":\"猫眼录屏-CatEyeScreenRecorder-v9.9-Windows-x64.zip\",\"browser_download_url\":\"https://github.com/example/cateye/releases/download/v9.9.0/update.zip\",\"digest\":\"sha256:abc\"}]}"; ReleaseInfo r=UpdateChecker.ParseReleaseForTest(json); Console.WriteLine(r==null?"NULL":"version="+r.Version+" asset="+r.AssetName+" url="+r.AssetUrl); } }
