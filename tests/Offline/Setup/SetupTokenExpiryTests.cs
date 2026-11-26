// SPDX-License-Identifier: MIT
using System.Net;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Setup;

public sealed class SetupTokenExpiryTests
{
    [Fact]
    public async Task A_hosted_token_that_expires_during_password_work_cannot_create_the_owner()
    {
        var time = new Kilnwarden.Offline.Auth.ManualTime(DateTimeOffset.UtcNow);
        await using var h = await AuthHarness.StartAsync(kap: false, time: time);
        var token = h.Auth.NewSetupToken(TimeSpan.FromMinutes(30), AuthService.SetupScope.Hosted);
        var slots = (SemaphoreSlim)typeof(PasswordHasher).GetField("_slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(h.Auth.Hasher)!;
        var held = slots.CurrentCount;
        for (var i = 0; i < held; i++) Assert.True(slots.Wait(0));
        Task<SignInResult> request;
        try
        {
            request = h.Auth.SetupOwnerAsync(token, "owner", LaneHost.Password, new ClientInfo(IPAddress.Loopback, "test") { Relayed = true }, default);
            Assert.False(request.IsCompleted);
            time.Advance(TimeSpan.FromMinutes(31));
        }
        finally { slots.Release(held); }
        Assert.Equal(401, Assert.IsType<SignInResult.Refused>(await request).Status);
        Assert.False(h.Auth.OwnerExists);
    }

}
