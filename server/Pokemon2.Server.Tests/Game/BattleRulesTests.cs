using Pokemon2.Server.Game;

namespace Pokemon2.Server.Tests.Game;

public sealed class BattleRulesTests
{
    [Fact]
    public void TryResolvePlayerAttack_WithBasicAttack_DamagesMonsterAndCounterAttacks()
    {
        var player = new BattleParticipant("p1", "tester", hp: 40, mp: 20, attack: 6);
        var monster = new MonsterState("m1", "풀벌레", new Position(1, 1), hp: 30, attack: 4);

        var accepted = BattleRules.TryResolvePlayerAttack(player, monster, "basic", 10, out var outcome, out var rejectReason);

        Assert.True(accepted);
        Assert.Equal("", rejectReason);
        Assert.Equal(12, outcome.Damage);
        Assert.Equal(18, monster.Hp);
        Assert.Equal(36, player.Hp);
        Assert.Equal(20, player.Mp);
        Assert.False(outcome.MonsterDefeated);
    }

    [Fact]
    public void TryResolvePlayerAttack_WithNotEnoughMp_RejectsWithoutChangingState()
    {
        var player = new BattleParticipant("p1", "tester", hp: 40, mp: 3, attack: 6);
        var monster = new MonsterState("m1", "풀벌레", new Position(1, 1), hp: 30, attack: 4);

        var accepted = BattleRules.TryResolvePlayerAttack(player, monster, "ember", 10, out _, out var rejectReason);

        Assert.False(accepted);
        Assert.Equal("not_enough_mp", rejectReason);
        Assert.Equal(30, monster.Hp);
        Assert.Equal(3, player.Mp);
    }

    [Fact]
    public void TryResolvePlayerAttack_WithSkillCooldown_RejectsSecondSkill()
    {
        var player = new BattleParticipant("p1", "tester", hp: 40, mp: 20, attack: 6);
        var monster = new MonsterState("m1", "풀벌레", new Position(1, 1), hp: 60, attack: 4);

        var first = BattleRules.TryResolvePlayerAttack(player, monster, "ember", 10, out _, out _);
        var second = BattleRules.TryResolvePlayerAttack(player, monster, "ember", 11, out _, out var rejectReason);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal("skill_cooldown", rejectReason);
    }

    [Fact]
    public void TryResolvePlayerAttack_WhenDamageDefeatsMonster_SkipsCounterAttack()
    {
        var player = new BattleParticipant("p1", "tester", hp: 40, mp: 20, attack: 6);
        var monster = new MonsterState("m1", "풀벌레", new Position(1, 1), hp: 10, attack: 4);

        var accepted = BattleRules.TryResolvePlayerAttack(player, monster, "basic", 10, out var outcome, out _);

        Assert.True(accepted);
        Assert.True(outcome.MonsterDefeated);
        Assert.Equal(0, monster.Hp);
        Assert.Equal(40, player.Hp);
    }
}
