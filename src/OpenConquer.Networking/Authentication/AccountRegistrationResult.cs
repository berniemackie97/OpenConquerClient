namespace OpenConquer.Networking.Authentication;

/// <summary>Result codes of the OpenConquer account-registration response (type 0xF002).</summary>
internal enum AccountRegistrationResult : uint
{
    Created = 0,
    InvalidRequest = 1,
    AccountNameTaken = 2,
    Failed = 3,
    Unavailable = 4,
}
