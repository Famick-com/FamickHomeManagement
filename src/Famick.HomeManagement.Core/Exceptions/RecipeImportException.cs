namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// A recipe page couldn't be imported. <see cref="Code"/> says why, for a client to show its own message;
/// nothing was saved.
/// </summary>
public class RecipeImportException : DomainException
{
    public const string NoRecipeFound = "NO_RECIPE_FOUND";
    public const string PageUnreachable = "PAGE_UNREACHABLE";
    public const string PageTimedOut = "PAGE_TIMED_OUT";
    public const string UrlNotAllowed = "URL_NOT_ALLOWED";
    public const string NotAWebPage = "NOT_A_WEB_PAGE";
    public const string PageTooLarge = "PAGE_TOO_LARGE";

    public string Code { get; }

    public RecipeImportException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// The household already has a recipe imported from the same page.
/// </summary>
public class RecipeAlreadyImportedException : DomainException
{
    public const string Code = "RECIPE_ALREADY_IMPORTED";

    public Guid ExistingRecipeId { get; }

    public RecipeAlreadyImportedException(Guid existingRecipeId)
        : base("This recipe has already been imported")
    {
        ExistingRecipeId = existingRecipeId;
    }
}
