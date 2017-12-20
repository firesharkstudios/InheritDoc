Allows adding `<inheritdoc/>` tags to XML comments in C# source code to inherit XML comments from base classes, interfaces, and similar methods. **Eliminates** unwanted copying and pasting of duplicate XML comments and **automatically** keeps XML comments synchronized.

XML comments (starting with `///`) are compiled into XML documentation files for each assembly by the normal build process. **InheritDoc** post processes these XML documentation files to inherit XML comments as needed. This approach makes the XML comments **compatible** with other documentation tools, **accessible** by Intellisense, and **packagable** into a Nuget package if distributing a library.

This is the command line version (InheritDocLib contains the same functionality in .NET assembly for programmatic access).

See [www.inheritdoc.io](https://www.inheritdoc.io/) for more details.

