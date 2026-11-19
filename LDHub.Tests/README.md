# LDHub tests

Run all tests from the repository folder:

    dotnet test LDHub.Tests

First time only, add the project to the solution (optional, lets Visual Studio list the tests):

    dotnet sln add LDHub.Tests/LDHub.Tests.csproj

The tests use an in-memory database, so they need no MySQL and never touch real data.
