using System.IO;
using System.Reflection;
using ShineProCS.Core.Interfaces;

namespace ShineProCS.Core.Services;

/// <summary>
/// 策略加载器
/// 支持从内置程序集和外部插件目录加载策略
/// </summary>
public class StrategyLoader
{
    private readonly string _pluginPath;
    private readonly List<ISkillStrategy> _strategies = [];
    private readonly Dictionary<string, StrategyInfo> _strategyInfos = [];

    /// <summary>
    /// 策略信息
    /// </summary>
    public class StrategyInfo
    {
        /// <summary>
        /// 策略唯一标识符
        /// </summary>
        public string Id { get; set; } = "";
        
        /// <summary>
        /// 策略名称
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// 策略描述
        /// </summary>
        public string Description { get; set; } = "";
        
        /// <summary>
        /// 策略版本
        /// </summary>
        public string Version { get; set; } = "";
        
        /// <summary>
        /// 策略作者
        /// </summary>
        public string Author { get; set; } = "";
        
        /// <summary>
        /// 是否为内置策略
        /// </summary>
        public bool IsBuiltIn { get; set; }
        
        /// <summary>
        /// 策略实例
        /// </summary>
        public ISkillStrategy? Instance { get; set; }
    }

    /// <summary>
    /// 创建策略加载器
    /// </summary>
    /// <param name="pluginPath">插件目录路径，默认为应用目录下的 plugins/strategies</param>
    public StrategyLoader(string? pluginPath = null)
    {
        _pluginPath = pluginPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "strategies");
    }

    /// <summary>
    /// 加载所有策略（内置 + 插件）
    /// </summary>
    /// <returns>加载的策略列表</returns>
    public List<ISkillStrategy> LoadAllStrategies()
    {
        _strategies.Clear();
        _strategyInfos.Clear();

        // 1. 加载内置策略
        LoadBuiltInStrategies();

        // 2. 加载插件策略
        LoadPluginStrategies();

        // 3. 按优先级排序
        _strategies.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        return _strategies;
    }

    /// <summary>
    /// 加载内置策略
    /// </summary>
    private void LoadBuiltInStrategies()
    {
        var assembly = Assembly.GetExecutingAssembly();
        LoadStrategiesFromAssembly(assembly, isBuiltIn: true);
    }

    /// <summary>
    /// 从插件目录加载策略
    /// </summary>
    private void LoadPluginStrategies()
    {
        if (!Directory.Exists(_pluginPath))
        {
            // 创建插件目录
            try
            {
                Directory.CreateDirectory(_pluginPath);
                CreatePluginReadme();
            }
            catch { }
            return;
        }

        foreach (var dllPath in Directory.GetFiles(_pluginPath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                LoadStrategiesFromAssembly(assembly, isBuiltIn: false);
            }
            catch (Exception ex)
            {
                // 记录加载失败的插件
                System.Diagnostics.Debug.WriteLine($"加载策略插件失败: {dllPath}, 错误: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 从程序集加载策略
    /// </summary>
    private void LoadStrategiesFromAssembly(Assembly assembly, bool isBuiltIn)
    {
        try
        {
            var strategyTypes = assembly.GetTypes()
                .Where(t => typeof(ISkillStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in strategyTypes)
            {
                try
                {
                    var instance = Activator.CreateInstance(type) as ISkillStrategy;
                    if (instance == null) continue;

                    // 获取元数据
                    var metadata = type.GetCustomAttribute<StrategyMetadataAttribute>();
                    var info = new StrategyInfo
                    {
                        Id = metadata?.Id ?? type.Name,
                        Name = metadata?.Name ?? instance.Name,
                        Description = metadata?.Description ?? instance.Description,
                        Version = metadata?.Version ?? "1.0.0",
                        Author = metadata?.Author ?? "",
                        IsBuiltIn = isBuiltIn,
                        Instance = instance
                    };

                    // 避免重复加载
                    if (!_strategyInfos.ContainsKey(info.Id))
                    {
                        _strategies.Add(instance);
                        _strategyInfos[info.Id] = info;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"实例化策略失败: {type.Name}, 错误: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"扫描程序集策略失败: {assembly.FullName}, 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建插件目录说明文件
    /// </summary>
    private void CreatePluginReadme()
    {
        var readmePath = Path.Combine(_pluginPath, "README.md");
        if (File.Exists(readmePath)) return;

        var content = """
            # 策略插件目录

            将自定义策略 DLL 文件放置在此目录，程序启动时会自动加载。

            ## 创建自定义策略

            1. 创建一个 .NET 类库项目
            2. 引用 ShineProCS 的 Core 程序集
            3. 实现 `ISkillStrategy` 接口
            4. 添加 `[StrategyMetadata]` 特性（可选）
            5. 编译后将 DLL 复制到此目录

            ## 示例代码

            ```csharp
            using ShineProCS.Core.Interfaces;
            using ShineProCS.Models;

            namespace MyStrategies;

            [StrategyMetadata("my-strategy", "我的策略", 
                Description = "自定义技能选择逻辑", 
                Version = "1.0.0", 
                Author = "YourName")]
            public class MyCustomStrategy : ISkillStrategy
            {
                public string Name => "我的策略";
                public string Description => "自定义技能选择逻辑";
                public int Priority => 50;

                public bool CanExecute(StrategyContext context) => true;

                public SkillRuntimeState? SelectSkill(StrategyContext context)
                {
                    // 实现你的技能选择逻辑
                    return context.SkillStates
                        .Where(s => s.Config.Enabled && s.IsAvailable)
                        .OrderByDescending(s => s.Config.Priority)
                        .FirstOrDefault();
                }
            }
            ```

            ## 注意事项

            - 策略 DLL 必须与主程序使用相同的 .NET 版本
            - 策略类必须有无参构造函数
            - 策略 ID 必须唯一，重复的 ID 会被忽略
            """;

        try
        {
            File.WriteAllText(readmePath, content);
        }
        catch { }
    }

    /// <summary>
    /// 获取所有已加载的策略信息
    /// </summary>
    public IReadOnlyDictionary<string, StrategyInfo> GetStrategyInfos() => _strategyInfos;

    /// <summary>
    /// 获取指定ID的策略
    /// </summary>
    public ISkillStrategy? GetStrategy(string id)
    {
        return _strategyInfos.TryGetValue(id, out var info) ? info.Instance : null;
    }

    /// <summary>
    /// 重新加载所有策略
    /// </summary>
    public List<ISkillStrategy> ReloadStrategies() => LoadAllStrategies();
}
