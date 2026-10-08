# myra-export

`myra-export` is a command line tool that exports a Myra `.xmmp` UI project to C# code.

## Installation

```bash
dotnet tool install --global myra-export
```

## Update

```bash
dotnet tool update --global myra-export
```

## Running

```bash
myra-export <file.xmmp>
```

The tool creates two files next to the `.xmmp` file:

- `<ClassName>.cs` - the main file with the initialization code placeholder.
- `<ClassName>.Generated.cs` - the designer file with the generated widget hierarchy and property initialization.

If the main file already exists it is never overwritten, so any manual modifications are preserved. The designer file is always regenerated.

The generated class name, namespace and output directory are taken from the project's `ExportOptions`. When they are not set, sensible defaults are used: the namespace defaults to `MyraExport`, the class name to the file name without extension, and the output directory to the folder containing the `.xmmp` file.

## Options

| Option | Description |
| ------ | ----------- |
| `--namespace <ns>` | Namespace of the generated classes. Overrides the project's `ExportOptions`. |
| `--class <name>` | Name of the generated classes. Overrides the project's `ExportOptions`. |
| `--output <dir>` | Output directory. Overrides the project's `ExportOptions`. |
| `--help` | Shows the usage information. |

## Example

Export `form.xmmp` to `MyGame.UI` namespace with the class `MainForm` in the current directory:

```bash
myra-export form.xmmp --namespace MyGame.UI --class MainForm
```

The following files are written:

```
Success. Following files had been written:
.\MainForm.cs
.\MainForm.Generated.cs
```