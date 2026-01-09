namespace ShineProCS.Core.Interfaces;

/// <summary>
/// Buff检测接口
/// 用于检测Buff是否存在
/// </summary>
public interface IBuffChecker
{
    /// <summary>
    /// 检查指定Buff是否存在
    /// </summary>
    /// <param name="buffName">Buff名称</param>
    /// <returns>Buff是否存在</returns>
    bool CheckBuffExists(string buffName);
}
