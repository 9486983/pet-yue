namespace MyPersonalTool.Sdk;

/// <summary>宠物动画动作 —— 插件通过此枚举控制宠物表现</summary>
public enum PetAnimation
{
    /// <summary>待机 / 空闲</summary>
    Idle = 0,
    /// <summary>思考 / 处理中（计算、查询等耗时操作）</summary>
    Think = 1,
    /// <summary>开心 / 正面反馈</summary>
    Happy = 2,
    /// <summary>挥手 / 打招呼</summary>
    Wave = 3,
    /// <summary>跳跃 / 反应</summary>
    Jump = 4,
    /// <summary>行动完成 / 操作成功</summary>
    Complete = 5,
}
