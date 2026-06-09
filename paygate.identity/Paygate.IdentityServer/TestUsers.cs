using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Test;

namespace Paygate.IdentityServer;

// Three in-memory users, one per role, supporting the maker-checker study scenario:
//   - clerk     creates payments            (PaymentInitiator)
//   - approver  approves / rejects payments (PaymentApprover)  â€” must NOT be the creator
//   - auditor   read-only view              (Auditor)
//
// SubjectId is the stable `sub` claim the API uses as the actor id (created_by / approver).
public static class TestUsers
{
    public static List<TestUser> Users =>
    [
        new TestUser
        {
            SubjectId = "clerk",
            Username = "clerk",
            Password = "Pass123$",
            Claims =
            {
                new Claim(JwtClaimTypes.Name, "Clara Clerk"),
                new Claim(JwtClaimTypes.Role, "PaymentInitiator"),
            },
        },
        new TestUser
        {
            SubjectId = "approver",
            Username = "approver",
            Password = "Pass123$",
            Claims =
            {
                new Claim(JwtClaimTypes.Name, "Adam Approver"),
                new Claim(JwtClaimTypes.Role, "PaymentApprover"),
            },
        },
        new TestUser
        {
            SubjectId = "auditor",
            Username = "auditor",
            Password = "Pass123$",
            Claims =
            {
                new Claim(JwtClaimTypes.Name, "Aria Auditor"),
                new Claim(JwtClaimTypes.Role, "Auditor"),
            },
        },
    ];
}
