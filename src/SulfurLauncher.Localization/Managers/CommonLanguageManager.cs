using Irihi.Lingua;

namespace SulfurLauncher.Localization;

[LinguaManager("./Localization/zh-CN/Common.json")]
public partial class CommonLanguageManager
{
    static CommonLanguageManager()
    {
        LocalizationService.Register(Instance);
    }
}
