using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Lets the test project verify internal types directly (repository, evaluators, engine...) without
// widening the plugin assembly's public surface.
[assembly: InternalsVisibleTo("DevEn.Xrm.EntityValidation.Tests")]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("DevEn.Xrm.EntityValidation")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("ReviOS 11 25.10")]
[assembly: AssemblyProduct("DevEn.Xrm.EntityValidation")]
[assembly: AssemblyCopyright("Copyright © ReviOS 11 25.10 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("88c0b325-0204-46bf-8af1-61eef507d150")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
