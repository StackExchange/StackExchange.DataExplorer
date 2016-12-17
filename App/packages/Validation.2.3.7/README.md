Validation
==========

*Method input validation and runtime checks that report errors or throw
exceptions when failures are detected.*

[![Build status](https://ci.appveyor.com/api/projects/status/nhrah957le2jri3q?svg=true)](https://ci.appveyor.com/project/AArnott/validation)
[![NuGet package](https://img.shields.io/nuget/v/Validation.svg)](https://nuget.org/packages/Validation)

This project is available as the [Validation][1] NuGet package.

Basic input validation via the `Requires` class throws an ArgumentException.

    Requires.NotNull(arg1, "arg1");
    Requires.NotNullOrEmpty(arg2, "arg2");

State validation via the `Verify` class throws an InvalidOperationException.

    Verify.Operation(condition, "some error occurred.");

Internal integrity checks via the `Assumes` class throws an
InternalErrorException.

    Assumes.True(condition, "some error");

Warning signs that should not throw exceptions via the `Report` class.

    Report.IfNot(condition, "some error");

Code Snippets
-------------

Make writing input validation especially convenient with [code snippets][2].
Run the tools\install_snippets.cmd script within this package to copy the code snippets
into your `Documents\Visual Studio 201x\Code Snippets\Visual C#\My Code Snippets`
folder(s) and just type the first few letters of the code snippet name to get
auto-completion assisted input validation.

Note that if you have Resharper installed, code snippets don't appear in
auto-completion lists so you may have to press `Ctrl+J` after the first few letters
of the code snippet name for it to become available.

Example:

    private void SomeMethod(string input) {
        rnne<TAB>
    }

Expands to

    private void SomeMethod(string input) {
        Requires.NotNullOrEmpty(paramName, nameof(paramName));
    }

And the first `paramName` is selected. Simply type the actual parameter name
(Intellisense will auto-complete for you) and then the quoted paramName name
will automatically be changed to match.

The two snippets are `rnn` and `rnne`
which expand to check for null inputs or null-or-empty inputs, respectively.

[1]: http://nuget.org/packages/Validation "Validation NuGet package"
