using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MVC_App.BlazorClient.Identity;

/// <summary>
/// Mirror of <c>MVC_App/Models/Identity/ApplicationUser.cs</c>.
///
/// Both types are stock Identity with no added members and no model customisation, so this
/// context maps onto exactly the same schema as the MVC app's and both apps can point at the one
/// MVC_AppIdentity database. That is the point: a user registered in the MVC app signs in here
/// with the same credentials, and no migration is needed because neither app owns a different
/// shape of the tables.
///
/// If either side ever adds a property to its user type, the two stop matching and this needs to
/// become a shared project rather than a copy.
/// </summary>
public class ApplicationUser : IdentityUser
{
}

/// <inheritdoc cref="ApplicationUser"/>
public class IdentityContext : IdentityDbContext<ApplicationUser>
{
    public IdentityContext(DbContextOptions<IdentityContext> options)
        : base(options)
    {
    }
}
