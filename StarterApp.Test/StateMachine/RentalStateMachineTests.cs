using StarterApp.Database.StateMachine;

namespace StarterApp.Test.StateMachine;

public class RentalStateMachineTests
{
    [Theory]
    [InlineData("Pending", "Approved")]
    [InlineData("Pending", "Denied")]
    [InlineData("Pending", "Cancelled")]
    [InlineData("Approved", "Active")]
    [InlineData("Approved", "Cancelled")]
    [InlineData("Active", "Returned")]
    public void CanTransition_ValidTransition_ReturnsTrue(string from, string to)
    {
        Assert.True(RentalStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData("Pending", "Returned")]
    [InlineData("Pending", "Active")]
    [InlineData("Denied", "Approved")]
    [InlineData("Returned", "Active")]
    [InlineData("Active", "Pending")]
    public void CanTransition_InvalidTransition_ReturnsFalse(string from, string to)
    {
        Assert.False(RentalStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void CanTransition_UnknownFromState_ReturnsFalse()
    {
        Assert.False(RentalStateMachine.CanTransition("Unknown", "Approved"));
    }

    [Fact]
    public void GetAllowedTransitions_Pending_ReturnsThreeOptions()
    {
        var allowed = RentalStateMachine.GetAllowedTransitions("Pending");
        Assert.Contains("Approved", allowed);
        Assert.Contains("Denied", allowed);
        Assert.Contains("Cancelled", allowed);
    }

    [Fact]
    public void GetAllowedTransitions_TerminalState_ReturnsEmpty()
    {
        var allowed = RentalStateMachine.GetAllowedTransitions("Returned");
        Assert.Empty(allowed);
    }

    [Theory]
    [InlineData("Pending",  "Approved",  RentalStateMachine.Actor.Owner,     true)]
    [InlineData("Pending",  "Denied",    RentalStateMachine.Actor.Owner,     true)]
    [InlineData("Pending",  "Cancelled", RentalStateMachine.Actor.Requestor, true)]
    [InlineData("Pending",  "Cancelled", RentalStateMachine.Actor.Owner,     false)]
    [InlineData("Approved", "Active",    RentalStateMachine.Actor.Owner,     true)]
    [InlineData("Approved", "Active",    RentalStateMachine.Actor.Requestor, false)]
    [InlineData("Approved", "Cancelled", RentalStateMachine.Actor.Requestor, true)]
    public void CanTransitionAs_RespectsActorRules(string from, string to, RentalStateMachine.Actor actor, bool expected)
    {
        Assert.Equal(expected, RentalStateMachine.CanTransitionAs(from, to, actor));
    }
}