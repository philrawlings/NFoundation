# New Source Link Project Instructions

Here's how you can create a simple C# project, add a dummy `.cs` file, and then build a source-only NuGet package with Source Link.

### 1. Create a New .NET Class Library Project

First, create a new class library project to hold your source code.

Open a terminal/command prompt and run the following command:

```bash
dotnet new classlib -n BankProject
```

This will create a new directory called `BankProject` with a class library template.

Navigate to the project folder:

```bash
cd BankProject
```

### 2. Add a Dummy `.cs` File

Let's add a simple class file to the project. In the `BankProject` directory, create a new file called `BankAccount.cs` and add some dummy code to it.

#### BankAccount.cs:
```csharp
namespace BankProject
{
    public class BankAccount
    {
        public decimal Balance { get; private set; }

        public BankAccount(decimal initialBalance)
        {
            Balance = initialBalance;
        }

        public void Deposit(decimal amount)
        {
            if (amount > 0)
                Balance += amount;
        }

        public bool Withdraw(decimal amount)
        {
            if (amount > 0 && Balance >= amount)
            {
                Balance -= amount;
                return true;
            }
            return false;
        }
    }
}
```

This is just a simple class representing a bank account with a deposit and withdraw function.

### 3. Add Source Link NuGet Package

Now, to add Source Link support, you'll need to install the `Microsoft.SourceLink.GitHub` NuGet package (assuming you're using GitHub).

Run the following command to add the package:

```bash
dotnet add package Microsoft.SourceLink.GitHub --version 3.0.0
```

### 4. Modify `.csproj` to Include Source Files

Next, modify your `.csproj` file to enable the generation of the source link and package the source files.

Open the `.csproj` file (it should be named `BankProject.csproj`) and update it to the following:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageOutputPath>./nupkgs</PackageOutputPath> <!-- Where to store the generated NuGet package -->
    <IncludeSource>true</IncludeSource> <!-- Include source code in the package -->
    <IncludeSymbols>true</IncludeSymbols> <!-- Include symbols for debugging -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl> <!-- Publish the repository URL -->
    <PackageReleaseNotes>Initial release of the BankProject</PackageReleaseNotes>
  </PropertyGroup>

  <ItemGroup>
    <!-- Add the SourceLink NuGet package -->
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="3.0.0" />
  </ItemGroup>

</Project>
```

Here’s what these settings do:
- `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`: This ensures a NuGet package is created when you build the project.
- `<IncludeSource>true</IncludeSource>`: This ensures that the source code is included in the NuGet package.
- `<IncludeSymbols>true</IncludeSymbols>`: This adds the symbols (.pdb) to help with debugging.
- `<PublishRepositoryUrl>true</PublishRepositoryUrl>`: This embeds the repository URL, allowing Source Link to work with GitHub.

### 5. Build the Project and Generate the NuGet Package

Now, let's build the project and generate the NuGet package.

Run the following command:

```bash
dotnet pack
```

This will compile the project and create a `.nupkg` file in the `nupkgs` directory (or whatever directory you specified in the `.csproj` file).

You should see an output like this:

```
Microsoft (R) Build Engine version 17.3.2+0e9f71a56 for .NET
  Determining projects to restore...
  Restored /path/to/BankProject/BankProject.csproj (in 200 ms).
  /path/to/BankProject/BankProject.csproj: warning NU5104: 'IncludeSource' is deprecated and will be removed in a future version of NuGet. Please use 'IncludeSources' instead.
  Packaged /path/to/BankProject/BankProject.csproj to /path/to/BankProject/nupkgs/BankProject.1.0.0.nupkg.
```

The `.nupkg` file will include the source code and the symbols for debugging.

### 6. Verify the NuGet Package

You can open the generated `.nupkg` file to verify that the source files are included. One way to inspect it is by using `nuget` or a tool like 7-Zip to open the `.nupkg` file.

If everything is set up correctly, you should see the following in the `.nupkg`:
- A `lib` folder with the compiled assembly.
- A `src` folder with the source code files (`BankAccount.cs`).
- A `symbols` folder with `.pdb` files.

### 7. Push the Package (Optional)

If you want to push the package to a NuGet repository (like NuGet.org or your private feed), run the following command:

```bash
dotnet nuget push ./nupkgs/BankProject.1.0.0.nupkg --api-key <your-api-key> --source <nuget-feed-url>
```

Make sure to replace `<your-api-key>` and `<nuget-feed-url>` with your actual NuGet API key and feed URL.

---

Now you have a simple `BankProject` with a dummy `.cs` file (`BankAccount.cs`), Source Link enabled, and the project packaged as a NuGet package with source files included.

Let me know if you'd like more details on any of these steps!
