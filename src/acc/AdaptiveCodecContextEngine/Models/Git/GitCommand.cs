public readonly record struct GitCLI
{
    public const string Git = "git";
    public const string PerFileLogSearch =
        "log --reverse --format=\"COMMIT|%aI|%ae\" --name-only -M90 -- ";
    public const string PerRepoLogSearch =
        "log --reverse --format=\"COMMIT|%aI|%ae\" --name-only -M90";
}
