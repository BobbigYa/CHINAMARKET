using System.Globalization;
using Tpos.Desktop.Mvvm;

namespace Tpos.Desktop.Services;

public sealed class LocalizationService : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, string> Chinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AppName"] = "TPOS^",
            ["BrandEdition"] = "RETAIL WORKSPACE",
            ["BrandKicker"] = "简洁 · 安全 · 专注",
            ["BrandHeadline"] = "让管理，更简单。",
            ["BrandText"] = "专为日常经营打造的安全工作空间，清晰、可靠，从容开始每一天。",
            ["OfflineNote"] = "完全离线运行，无需连接互联网",
            ["LocalAccess"] = "本地安全访问",
            ["Welcome"] = "欢迎回来",
            ["LoginInstruction"] = "输入管理员密码以继续",
            ["AdminPassword"] = "管理员密码",
            ["PasswordPlaceholder"] = "输入密码",
            ["EnterSystem"] = "进入系统",
            ["Verifying"] = "正在验证…",
            ["ChangePassword"] = "修改密码",
            ["PrivacyNote"] = "密码仅用于本机验证，不会上传到外部服务",
            ["ShowPassword"] = "显示密码",
            ["HidePassword"] = "隐藏密码",
            ["RemainingAttempts"] = "还可尝试 {0} 次。",
            ["LockRemaining"] = "剩余锁定时间：{0}",
            ["SetupTitle"] = "创建管理员密码",
            ["SetupInstruction"] = "首次使用，请为本机设置一个仅您知道的管理员密码。",
            ["NewPassword"] = "新密码",
            ["ConfirmPassword"] = "再次输入密码",
            ["CreateAdministrator"] = "创建管理员并返回登录",
            ["CreatingAdministrator"] = "正在安全创建…",
            ["SetupSecurityNote"] = "密码至少 12 个字符。系统只保存 Argon2id 哈希，不保存明文密码。",
            ["ChangePasswordTitle"] = "修改管理员密码",
            ["CurrentPassword"] = "当前密码",
            ["ConfirmChange"] = "确认修改",
            ["Cancel"] = "取消",
            ["PasswordChanged"] = "密码已修改，请使用新密码登录。",
            ["AdministratorCreated"] = "管理员密码已创建，请重新输入密码登录。",
            ["Dashboard"] = "Dashboard",
            ["Navigation"] = "导航",
            ["Overview"] = "概览",
            ["DashboardWelcome"] = "欢迎回来",
            ["DashboardMessage"] = "您已安全登录 TPOS^。",
            ["ReadyTitle"] = "基础登录系统已就绪",
            ["ReadyMessage"] = "这是一个占位主界面。销售、商品、库存和支付功能尚未开发。",
            ["SessionVerified"] = "本地会话已验证",
            ["OfflineMode"] = "完全离线",
            ["Logout"] = "退出登录",
            ["Minimize"] = "最小化",
            ["Maximize"] = "最大化或还原",
            ["Close"] = "关闭",
            ["UnexpectedError"] = "操作未完成，请稍后重试。",
            ["Error.NotInitialized"] = "尚未创建管理员账户。",
            ["Error.AlreadyInitialized"] = "管理员账户已经存在，请直接登录。",
            ["Error.PasswordRequired"] = "请输入密码。",
            ["Error.PasswordTooShort"] = "密码至少需要 12 个字符。",
            ["Error.PasswordTooLong"] = "密码不能超过 128 个字符。",
            ["Error.PasswordMismatch"] = "两次输入的密码不一致。",
            ["Error.WeakPassword"] = "密码过于简单，请避免纯数字、空白或重复字符。",
            ["Error.InvalidPassword"] = "密码不正确，请重试。",
            ["Error.Locked"] = "连续失败 5 次，登录已暂时锁定。",
            ["Error.SamePassword"] = "新密码不能与当前密码相同。",
            ["Error.CredentialDataInvalid"] = "本地安全数据异常，无法验证。请检查安装或恢复备份。",
            ["Error.StorageError"] = "无法访问本地安全数据，请检查文件权限后重试。"
        };

    private static readonly IReadOnlyDictionary<string, string> Spanish =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AppName"] = "TPOS^",
            ["BrandEdition"] = "RETAIL WORKSPACE",
            ["BrandKicker"] = "SENCILLO · SEGURO · PROFESIONAL",
            ["BrandHeadline"] = "La gestión, más sencilla.",
            ["BrandText"] = "Un espacio de trabajo seguro para tu negocio diario: claro, fiable y siempre listo.",
            ["OfflineNote"] = "Funciona sin conexión. No necesita Internet",
            ["LocalAccess"] = "Acceso local seguro",
            ["Welcome"] = "Bienvenido",
            ["LoginInstruction"] = "Introduce la contraseña de administrador para continuar",
            ["AdminPassword"] = "Contraseña de administrador",
            ["PasswordPlaceholder"] = "Introduce la contraseña",
            ["EnterSystem"] = "Entrar al sistema",
            ["Verifying"] = "Verificando…",
            ["ChangePassword"] = "Cambiar contraseña",
            ["PrivacyNote"] = "La contraseña solo se verifica en este equipo; no se envía a servicios externos",
            ["ShowPassword"] = "Mostrar contraseña",
            ["HidePassword"] = "Ocultar contraseña",
            ["RemainingAttempts"] = "Quedan {0} intentos.",
            ["LockRemaining"] = "Tiempo de bloqueo restante: {0}",
            ["SetupTitle"] = "Crea la contraseña de administrador",
            ["SetupInstruction"] = "Es el primer uso. Crea una contraseña local que solo tú conozcas.",
            ["NewPassword"] = "Nueva contraseña",
            ["ConfirmPassword"] = "Repite la contraseña",
            ["CreateAdministrator"] = "Crear administrador y volver al acceso",
            ["CreatingAdministrator"] = "Creando de forma segura…",
            ["SetupSecurityNote"] = "Usa al menos 12 caracteres. Solo se guarda un hash Argon2id, nunca la contraseña.",
            ["ChangePasswordTitle"] = "Cambiar la contraseña de administrador",
            ["CurrentPassword"] = "Contraseña actual",
            ["ConfirmChange"] = "Confirmar cambio",
            ["Cancel"] = "Cancelar",
            ["PasswordChanged"] = "Contraseña cambiada. Inicia sesión con la nueva contraseña.",
            ["AdministratorCreated"] = "Contraseña creada. Introdúcela de nuevo para iniciar sesión.",
            ["Dashboard"] = "Dashboard",
            ["Navigation"] = "Navegación",
            ["Overview"] = "Resumen",
            ["DashboardWelcome"] = "Bienvenido",
            ["DashboardMessage"] = "Has iniciado sesión en TPOS^ de forma segura.",
            ["ReadyTitle"] = "El sistema de acceso está listo",
            ["ReadyMessage"] = "Esta es una pantalla provisional. Ventas, productos, inventario y pagos aún no están desarrollados.",
            ["SessionVerified"] = "Sesión local verificada",
            ["OfflineMode"] = "Sin conexión",
            ["Logout"] = "Cerrar sesión",
            ["Minimize"] = "Minimizar",
            ["Maximize"] = "Maximizar o restaurar",
            ["Close"] = "Cerrar",
            ["UnexpectedError"] = "No se pudo completar la operación. Inténtalo de nuevo.",
            ["Error.NotInitialized"] = "Todavía no existe una cuenta de administrador.",
            ["Error.AlreadyInitialized"] = "La cuenta de administrador ya existe. Inicia sesión.",
            ["Error.PasswordRequired"] = "Introduce la contraseña.",
            ["Error.PasswordTooShort"] = "La contraseña debe tener al menos 12 caracteres.",
            ["Error.PasswordTooLong"] = "La contraseña no puede superar los 128 caracteres.",
            ["Error.PasswordMismatch"] = "Las contraseñas no coinciden.",
            ["Error.WeakPassword"] = "La contraseña es demasiado simple. Evita usar solo números, espacios o caracteres repetidos.",
            ["Error.InvalidPassword"] = "La contraseña es incorrecta. Inténtalo de nuevo.",
            ["Error.Locked"] = "Tras 5 intentos fallidos, el acceso se ha bloqueado temporalmente.",
            ["Error.SamePassword"] = "La nueva contraseña debe ser diferente de la actual.",
            ["Error.CredentialDataInvalid"] = "Los datos locales de seguridad no son válidos. Revisa la instalación o restaura una copia.",
            ["Error.StorageError"] = "No se puede acceder a los datos locales de seguridad. Revisa los permisos del archivo."
        };

    private string _language = "zh";

    public LocalizationService()
    {
        SetLanguageCommand = new RelayCommand<string>(SetLanguage);
    }

    public event EventHandler? LanguageChanged;

    public string Language
    {
        get => _language;
        private set => SetProperty(ref _language, value);
    }

    public RelayCommand<string> SetLanguageCommand { get; }

    public string this[string key] => Get(key);

    public void SetLanguage(string? language)
    {
        var normalized = string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "es-ES", StringComparison.OrdinalIgnoreCase)
            ? "es"
            : "zh";

        if (Language == normalized)
        {
            return;
        }

        Language = normalized;
        CultureInfo.CurrentUICulture = new CultureInfo(normalized == "es" ? "es-ES" : "zh-CN");
        OnPropertyChanged("Item[]");
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        var dictionary = Language == "es" ? Spanish : Chinese;
        return dictionary.TryGetValue(key, out var value) ? value : Chinese[key];
    }
}
