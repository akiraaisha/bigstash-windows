﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.34209
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace BigStash.WPF.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "12.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ApplicationDataFolder {
            get {
                return ((string)(this["ApplicationDataFolder"]));
            }
            set {
                this["ApplicationDataFolder"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("BigStash")]
        public string ApplicationName {
            get {
                return ((string)(this["ApplicationName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("preferences.djf")]
        public string SettingsFileName {
            get {
                return ((string)(this["SettingsFileName"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string SettingsFilePath {
            get {
                return ((string)(this["SettingsFilePath"]));
            }
            set {
                this["SettingsFilePath"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("uploads")]
        public string UploadsFolderName {
            get {
                return ((string)(this["UploadsFolderName"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string UploadsFolderPath {
            get {
                return ((string)(this["UploadsFolderPath"]));
            }
            set {
                this["UploadsFolderPath"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://www.bigstash.co/api/v1/")]
        public string ServerBaseAddress {
            get {
                return ((string)(this["ServerBaseAddress"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("endpoint.txt")]
        public string EndpointFileName {
            get {
                return ((string)(this["EndpointFileName"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string EndpointFilePath {
            get {
                return ((string)(this["EndpointFilePath"]));
            }
            set {
                this["EndpointFilePath"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("BigStash")]
        public string ApplicationFullName {
            get {
                return ((string)(this["ApplicationFullName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Log.txt")]
        public string LogFileName {
            get {
                return ((string)(this["LogFileName"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string LogFilePath {
            get {
                return ((string)(this["LogFilePath"]));
            }
            set {
                this["LogFilePath"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(".djf")]
        public string BigStashJsonFormat {
            get {
                return ((string)(this["BigStashJsonFormat"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool MinimizeOnClose {
            get {
                return ((bool)(this["MinimizeOnClose"]));
            }
            set {
                this["MinimizeOnClose"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool IsFirstLogin {
            get {
                return ((bool)(this["IsFirstLogin"]));
            }
            set {
                this["IsFirstLogin"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool RestartAfterUpdate {
            get {
                return ((bool)(this["RestartAfterUpdate"]));
            }
            set {
                this["RestartAfterUpdate"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool DoAutomaticUpdates {
            get {
                return ((bool)(this["DoAutomaticUpdates"]));
            }
            set {
                this["DoAutomaticUpdates"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string DebugServerBaseAddress {
            get {
                return ((string)(this["DebugServerBaseAddress"]));
            }
            set {
                this["DebugServerBaseAddress"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Deepfreeze.io")]
        public string DeepfreezeApplicationFolderName {
            get {
                return ((string)(this["DeepfreezeApplicationFolderName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://www.bigstash.co/")]
        public string BigStashURL {
            get {
                return ((string)(this["BigStashURL"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string BigStashBlogURL {
            get {
                return ((string)(this["BigStashBlogURL"]));
            }
            set {
                this["BigStashBlogURL"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://www.bigstash.co/faq/#Why-are-some-of-my-files-not-being-uploaded")]
        public string BigStashNameRulesFAQURL {
            get {
                return ((string)(this["BigStashNameRulesFAQURL"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool VerboseDebugLogging {
            get {
                return ((bool)(this["VerboseDebugLogging"]));
            }
            set {
                this["VerboseDebugLogging"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool BigStashUpdateMessageShown {
            get {
                return ((bool)(this["BigStashUpdateMessageShown"]));
            }
            set {
                this["BigStashUpdateMessageShown"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://www.bigstash.co/apps/windows/")]
        public string BigStashUpdateURL {
            get {
                return ((string)(this["BigStashUpdateURL"]));
            }
            set {
                this["BigStashUpdateURL"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("C:\\Squirrel\\")]
        public string LocalUpdateURL {
            get {
                return ((string)(this["LocalUpdateURL"]));
            }
            set {
                this["LocalUpdateURL"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SettingsUpgradeRequired {
            get {
                return ((bool)(this["SettingsUpgradeRequired"]));
            }
            set {
                this["SettingsUpgradeRequired"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.DateTime LastNotificationDate {
            get {
                return ((global::System.DateTime)(this["LastNotificationDate"]));
            }
            set {
                this["LastNotificationDate"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool RunOnStartup {
            get {
                return ((bool)(this["RunOnStartup"]));
            }
            set {
                this["RunOnStartup"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MinimizeBallonTipShown {
            get {
                return ((bool)(this["MinimizeBallonTipShown"]));
            }
            set {
                this["MinimizeBallonTipShown"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://www.bigstash.co/support/")]
        public string BigStashSupportURL {
            get {
                return ((string)(this["BigStashSupportURL"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AWSEndpointDefinition {
            get {
                return ((string)(this["AWSEndpointDefinition"]));
            }
            set {
                this["AWSEndpointDefinition"] = value;
            }
        }
    }
}
