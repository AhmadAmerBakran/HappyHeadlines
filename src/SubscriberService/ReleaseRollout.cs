using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SubscriberService;

public sealed record ReleaseRollout(bool Enabled, int Percentage)
{
    public bool Allows(string email)
    {
        if (!Enabled || Percentage <= 0)
        {
            return false;
        }

        if (Percentage >= 100)
        {
            return true;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        var bucket = BinaryPrimitives.ReadUInt32BigEndian(bytes) % 10_000;
        return bucket < Percentage * 100;
    }
}

public sealed record UpdateRolloutRequest(bool Enabled, int Percentage);
public sealed record SubscribeRequest(string? Email);
public sealed record SubscriberCreated(Guid Id, string Email, DateTime SubscribedAtUtc);
