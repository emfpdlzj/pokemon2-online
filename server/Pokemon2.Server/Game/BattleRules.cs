namespace Pokemon2.Server.Game;

public sealed record BattleSkill(string SkillId, string Name, int Power, int MpCost, long CooldownTicks, int Range);

public sealed class BattleParticipant
{
    public BattleParticipant(string playerId, string name, int hp = 40, int mp = 20, int attack = 6)
    {
        PlayerId = playerId;
        Name = name;
        Hp = hp;
        MaxHp = hp;
        Mp = mp;
        MaxMp = mp;
        Attack = attack;
    }

    public string PlayerId { get; }
    public string Name { get; }
    public int Hp { get; set; }
    public int MaxHp { get; }
    public int Mp { get; set; }
    public int MaxMp { get; }
    public int Attack { get; }
    public long NextSkillTick { get; set; }
}

public sealed class MonsterState
{
    public MonsterState(string monsterId, string name, Position position, int hp, int attack)
    {
        MonsterId = monsterId;
        Name = name;
        Position = position;
        Hp = hp;
        MaxHp = hp;
        Attack = attack;
    }

    public string MonsterId { get; }
    public string Name { get; }
    public Position Position { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; }
    public int Attack { get; }
    public long NextAiTick { get; set; }
    public bool IsAlive => Hp > 0;
}

public sealed record BattleActionOutcome(
    string SkillId,
    string SkillName,
    int Damage,
    int MonsterHp,
    int PlayerHp,
    int PlayerMp,
    bool MonsterDefeated);

public static class BattleRules
{
    private static readonly IReadOnlyDictionary<string, BattleSkill> Skills = new Dictionary<string, BattleSkill>(StringComparer.OrdinalIgnoreCase)
    {
        ["basic"] = new("basic", "공격", 6, 0, 0, 1),
        ["ember"] = new("ember", "필살기", 12, 5, 40, 2)
    };

    public static BattleSkill GetSkillOrDefault(string? skillId)
    {
        return !string.IsNullOrWhiteSpace(skillId) && Skills.TryGetValue(skillId, out var skill)
            ? skill
            : Skills["basic"];
    }

    public static bool TryResolvePlayerAttack(
        BattleParticipant player,
        MonsterState monster,
        string? skillId,
        long serverTick,
        out BattleActionOutcome outcome,
        out string rejectReason)
    {
        outcome = default!;
        rejectReason = "";

        if (player.Hp <= 0)
        {
            rejectReason = "player_defeated";
            return false;
        }

        if (!monster.IsAlive)
        {
            rejectReason = "monster_defeated";
            return false;
        }

        var skill = GetSkillOrDefault(skillId);
        if (player.Mp < skill.MpCost)
        {
            rejectReason = "not_enough_mp";
            return false;
        }

        if (serverTick < player.NextSkillTick)
        {
            rejectReason = "skill_cooldown";
            return false;
        }

        player.Mp -= skill.MpCost;
        if (skill.CooldownTicks > 0)
        {
            player.NextSkillTick = serverTick + skill.CooldownTicks;
        }

        var damage = Math.Max(1, player.Attack + skill.Power);
        monster.Hp = Math.Max(0, monster.Hp - damage);
        var defeated = monster.Hp <= 0;
        if (!defeated)
        {
            player.Hp = Math.Max(0, player.Hp - monster.Attack);
        }

        outcome = new BattleActionOutcome(skill.SkillId, skill.Name, damage, monster.Hp, player.Hp, player.Mp, defeated);
        return true;
    }
}
