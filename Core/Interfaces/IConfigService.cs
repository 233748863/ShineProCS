using ShineProCS.Models;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 配置服务接口
/// 管理应用程序配置的加载、保存和变更通知
/// 参考 BetterGI 的配置服务设计
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 应用程序设置
    /// </summary>
    AppSettings AppSettings { get; }
    
    /// <summary>
    /// 技能配置列表
    /// </summary>
    List<SkillConfig> Skills { get; }
    
    /// <summary>
    /// 配置变更事件
    /// </summary>
    event Action<string>? ConfigChanged;
    
    /// <summary>
    /// 加载所有配置
    /// </summary>
    void Load();
    
    /// <summary>
    /// 保存所有配置（带防抖动）
    /// </summary>
    void Save();
    
    /// <summary>
    /// 立即保存所有配置（不防抖动）
    /// </summary>
    void SaveImmediate();
    
    /// <summary>
    /// 保存应用设置
    /// </summary>
    void SaveAppSettings();
    
    /// <summary>
    /// 保存技能配置
    /// </summary>
    void SaveSkills();
    
    /// <summary>
    /// 获取可用的配置方案列表
    /// </summary>
    List<string> GetAvailableProfiles();
    
    /// <summary>
    /// 切换配置方案
    /// </summary>
    /// <param name="profileName">方案名称</param>
    void SwitchProfile(string profileName);
    
    /// <summary>
    /// 创建新的配置方案
    /// </summary>
    /// <param name="profileName">方案名称</param>
    void CreateProfile(string profileName);
    
    /// <summary>
    /// 删除配置方案
    /// </summary>
    /// <param name="profileName">方案名称</param>
    void DeleteProfile(string profileName);
    
    /// <summary>
    /// 导出配置到文件
    /// </summary>
    /// <param name="exportPath">导出路径</param>
    /// <param name="includeTemplates">是否包含模板图片</param>
    void ExportConfig(string exportPath, bool includeTemplates = true);
    
    /// <summary>
    /// 从文件导入配置
    /// </summary>
    /// <param name="importPath">导入路径</param>
    /// <param name="overwrite">是否覆盖现有配置</param>
    /// <returns>导入结果信息</returns>
    string ImportConfig(string importPath, bool overwrite = false);
}
