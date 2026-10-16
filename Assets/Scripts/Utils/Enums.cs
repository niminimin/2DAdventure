public enum EnemyState
{
    Patrol,
    Chase,
    Skill,
    /// <summary>
    /// 死亡。终态，无出边。
    /// 【必须同时改 Enemy.SwitchState 的 switch】——
    /// 那里 default 分支会静默保持原状态，只加枚举不加 case 的话切换会无声失败。
    /// </summary>
    Dead
}

public enum SceneType
{
    Location, Menu
}

public enum PresistentType
{
    ReadAndWrite, DoNotPresist
}