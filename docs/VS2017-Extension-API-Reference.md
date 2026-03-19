# VS2017 Extension API — Tài liệu tổng hợp

> **Target:** Visual Studio 2017 (v15.x) · .NET 4.6 · AsyncPackage pattern  
> **Cập nhật:** 2024

---

## Mục lục

1. [Setup & AsyncPackage](#1-setup--asyncpackage)
2. [Output Window & Status Bar](#2-output-window--status-bar)
3. [Selection APIs](#3-selection-apis)
4. [Document & File APIs](#4-document--file-apis)
5. [Project & Solution APIs](#5-project--solution-apis)
6. [Menu & Command (VSCT + OleMenuCommand)](#6-menu--command)
7. [Toolbar & CommandBar](#7-toolbar--commandbar)
8. [Tool Window](#8-tool-window)
9. [Events](#9-events)
10. [Settings / Options Page](#10-settings--options-page)
11. [IntelliSense / Completion](#11-intellisense--completion)
12. [Text Adornment & Tagger](#12-text-adornment--tagger)
13. [NuGet References bắt buộc](#13-nuget-references-bắt-buộc)

---

## 1. Setup & AsyncPackage

### Tại sao dùng `AsyncPackage` thay `Package`?

| | `Package` (cũ) | `AsyncPackage` (VS2017+) |
|---|---|---|
| Thread | UI thread | Background thread |
| Risk | Deadlock khi gọi service | Không block UI |
| `GetService` | Đồng bộ | `await GetServiceAsync(...)` |
| Load flag | — | `AllowsBackgroundLoading = true` |

### Cài đặt NuGet tối thiểu

```xml
<!-- packages.config -->
<packages>
  <package id="Microsoft.VSSDK.BuildTools"  version="15.9.3084" developmentDependency="true" />
  <package id="EnvDTE"                      version="8.0.2" />
  <package id="EnvDTE80"                    version="8.0.3" />
  <package id="Microsoft.VisualStudio.Shell.Interop"      version="7.10.6040" />
  <package id="Microsoft.VisualStudio.TextManager.Interop" version="7.10.6040" />
</packages>
```

### Cấu trúc `.csproj` tối thiểu

```xml
<PropertyGroup>
  <!-- BẮT BUỘC cho VSIX project -->
  <ProjectTypeGuids>
    {82B43B9B-A64C-4715-B499-D71E9CA2179C};
    {FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}
  </ProjectTypeGuids>
  <TargetFrameworkVersion>v4.6</TargetFrameworkVersion>
  <MinimumVisualStudioVersion>15.0</MinimumVisualStudioVersion>
  <VSToolsPath Condition="'$(VSToolsPath)' == ''">
    $(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)
  </VSToolsPath>
  <GeneratePkgDefFile>true</GeneratePkgDefFile>
  <UseCodebase>true</UseCodebase>
</PropertyGroup>

<!-- Thêm search path để tìm VS managed assemblies -->
<PropertyGroup>
  <AssemblySearchPaths>
    $(AssemblySearchPaths);
    $(DevEnvDir)PublicAssemblies;
    $(DevEnvDir)PrivateAssemblies;
    $(DevEnvDir)CommonExtensions\Microsoft\Editor;
  </AssemblySearchPaths>
</PropertyGroup>

<!-- Import VSSDK targets (compile VSCT, tạo pkgdef, đóng gói vsix) -->
<Import Project="$(VSToolsPath)\VSSDK\Microsoft.VsSDK.targets" />
```

### Package Entry Point

```csharp
// PackageRegistration: đăng ký với VS registry
// AllowsBackgroundLoading = true — BẮT BUỘC cho AsyncPackage
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]

// ProvideAutoLoad: VS tự load package khi start (không cần click menu trước)
// BackgroundLoad: load trên background thread, không block VS startup
[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]

// ProvideMenuResource: chỉ định compiled menu resource
// "Menus.ctmenu" được VSCT compiler tạo từ .vsct file
[ProvideMenuResource("Menus.ctmenu", 1)]

// Guid: định danh duy nhất, phải khớp với .vsct và .vsixmanifest
[Guid("YOUR-PACKAGE-GUID-HERE")]
public sealed class MyPackage : AsyncPackage
{
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        // 1. Gọi base trước (vẫn trên background thread)
        await base.InitializeAsync(cancellationToken, progress);

        // 2. Báo tiến trình (hiện ở status bar của VS)
        progress?.Report(new ServiceProgressData("MyExt", "Loading...", 1, 3));

        // 3. Switch sang UI thread khi cần thao tác UI
        // Mọi thứ sau dòng này chạy trên UI thread
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // 4. GetServiceAsync — KHÔNG dùng GetService() đồng bộ ở đây
        var commandService = await GetServiceAsync(typeof(IMenuCommandService))
                             as OleMenuCommandService;

        // 5. Đăng ký commands
        await MyCommand.InitializeAsync(this);
    }
}
```

### GUIDs.cs — Tập trung quản lý GUIDs

```csharp
internal static class PackageGuids
{
    // Phải khớp với [Guid] attribute trên Package class
    // Và với GuidSymbol trong .vsct file
    public const string PackageGuidString = "A1B2C3D4-...";
    public static readonly Guid PackageGuid = new Guid(PackageGuidString);

    // CommandSet GUID: dùng cho tất cả CommandID trong extension
    public const string CommandSetGuidString = "B2C3D4E5-...";
    public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);
}

internal static class PackageIds
{
    // Phải khớp với IDSymbol values trong .vsct file
    public const int MyMenuGroup       = 0x1010;
    public const int cmdidMyCommand    = 0x0101;
}
```

---

## 2. Output Window & Status Bar

### Output Window — Tạo custom pane

```csharp
// SVsOutputWindow = service identifier (dùng để query)
// IVsOutputWindow = interface để tương tác
var outputWindow = await package.GetServiceAsync(typeof(SVsOutputWindow))
                  as IVsOutputWindow;

var paneGuid = new Guid("YOUR-PANE-GUID");

// CreatePane: tạo custom pane
//   fInitVisible = 1     : hiện ngay khi tạo
//   fClearWithSolution=0 : giữ nội dung khi đóng solution
outputWindow.CreatePane(ref paneGuid, "My Extension",
    fInitVisible: 1, fClearWithSolution: 0);

// GetPane: lấy reference để ghi
outputWindow.GetPane(ref paneGuid, out IVsOutputWindowPane pane);

// Sử dụng
pane.Activate();                    // đưa focus vào pane
pane.OutputString("Hello!\n");      // ghi text
pane.Clear();                       // xóa toàn bộ nội dung
```

### Status Bar

```csharp
// SVsStatusbar = service identifier
// IVsStatusbar = interface
var statusBar = await package.GetServiceAsync(typeof(SVsStatusbar))
                as IVsStatusbar;

// SetText: hiện text đơn giản ở status bar
statusBar.SetText("Build completed");

// Progress: thanh tiến trình
uint cookie = 0;
// Bắt đầu progress
statusBar.Progress(ref cookie, fInProgress: 1,
    pwszLabel: "Processing...", nComplete: 0, nTotal: 100);

// Cập nhật
statusBar.Progress(ref cookie, fInProgress: 1,
    pwszLabel: "Processing...", nComplete: 50, nTotal: 100);

// Kết thúc progress (nComplete == nTotal)
statusBar.Progress(ref cookie, fInProgress: 0,
    pwszLabel: "", nComplete: 0, nTotal: 0);

// Animation (icon xoay)
// KnownMonikers.BuildSolution = icon build
object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_Build;
statusBar.Animation(fOn: 1, pvIcon: ref icon);   // bật
statusBar.Animation(fOn: 0, pvIcon: ref icon);   // tắt
```

---

## 3. Selection APIs

### Đường dẫn lấy Selection

```
DTE (tầng 1 — COM, đơn giản):
  dte.ActiveDocument.Selection → cast sang TextSelection

IWpfTextView (tầng 2 — MEF, chính xác hơn):
  GetService(SVsTextManager) → IVsTextManager
  → GetActiveView() → IVsTextView
  → adaptersFactory.GetWpfTextView() → IWpfTextView
  → .Caret / .Selection / .TextBuffer
```

### TẦNG 1 — DTE TextSelection

```csharp
// Lấy DTE2 — entry point cho tất cả DTE APIs
// Dùng typeof(DTE) thay vì typeof(DTE2) vì service được đăng ký bằng DTE
var dte = ServiceProvider.GetService(typeof(DTE)) as DTE2;

// ActiveDocument.Selection là object (COM late binding)
// Phải cast sang TextSelection để dùng
var sel = (TextSelection)dte.ActiveDocument.Selection;

// ── Vị trí caret (1-based) ──────────────────────────────────
int line   = sel.CurrentLine;    // dòng caret đang đứng (bắt đầu từ 1)
int column = sel.CurrentColumn;  // cột hiển thị (tab = nhiều cột)

// ── Selection bounds ─────────────────────────────────────────
// AnchorPoint: nơi BẮT ĐẦU kéo chọn
// ActivePoint: nơi caret ĐANG ĐỨNG (có thể trước hoặc sau Anchor)
int anchorLine   = sel.AnchorPoint.Line;
int anchorCol    = sel.AnchorPoint.DisplayColumn;
int anchorOffset = sel.AnchorPoint.AbsoluteCharOffset; // offset từ đầu file (1-based)

int activeLine   = sel.ActivePoint.Line;
int activeOffset = sel.ActivePoint.AbsoluteCharOffset;

// TopLine / BottomLine: luôn Top <= Bottom (khác với Anchor/Active)
int topLine    = sel.TopLine;
int bottomLine = sel.BottomLine;

// ── Selected text ────────────────────────────────────────────
string selectedText = sel.Text;   // "" nếu không có selection
bool isEmpty        = string.IsNullOrEmpty(sel.Text);

// ── Selection mode ───────────────────────────────────────────
// vsSelectionModeStream = chọn thông thường
// vsSelectionModeBox    = column/box selection (Ctrl+Shift+Alt+Arrow)
var mode = sel.Mode;

// ── Manipulation ─────────────────────────────────────────────
sel.StartOfDocument(Extend: false);     // về đầu file (không chọn)
sel.EndOfDocument(Extend: false);       // về cuối file
sel.GotoLine(lineNumber: 42, Select: false); // nhảy đến dòng 42

// StartOfLine options:
//   vsStartOfLineOptionsFirstText   → ký tự đầu tiên không phải whitespace (smart home)
//   vsStartOfLineOptionsFirstColumn → cột 1 tuyệt đối
sel.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstText, Extend: false);
sel.EndOfLine(Extend: false);

sel.SelectLine();   // chọn toàn bộ dòng hiện tại
sel.SelectAll();    // chọn tất cả
sel.Collapse();     // bỏ chọn, caret về ActivePoint

// Di chuyển theo từ (word)
sel.WordRight(Extend: false, Count: 1); // sang phải 1 từ
sel.WordLeft(Extend: true, Count: 1);   // sang trái 1 từ, Extend=true → chọn từ đó

// Di chuyển đến vị trí cụ thể
sel.MoveToLineAndOffset(Line: 10, Offset: 5, Extend: false);

// Find & Replace trong document
bool found = sel.FindText("TODO", (int)vsFindOptions.vsFindOptionsMatchCase);
sel.ReplaceText("needle", "replacement", (int)vsFindOptions.vsFindOptionsNone);
```

### TẦNG 2 — IWpfTextView (Editor MEF API)

```csharp
// Bước 1: IComponentModel — MEF composition container của VS
// SComponentModel = service identifier
var componentModel = ServiceProvider.GetService(typeof(SComponentModel))
                     as IComponentModel;

// Bước 2: IVsEditorAdaptersFactoryService — bridge COM ↔ managed
// Dùng GetService<T>() của IComponentModel để lấy MEF export
var adapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();

// Bước 3: IVsTextManager — quản lý tất cả text views
var textManager = ServiceProvider.GetService(typeof(SVsTextManager))
                  as IVsTextManager;

// Bước 4: GetActiveView → IVsTextView (COM interface)
// fMustHaveFocus=1 : chỉ lấy view đang có keyboard focus
textManager.GetActiveView(fMustHaveFocus: 1, pBuffer: null,
                          ppView: out IVsTextView vsView);

// Bước 5: Convert IVsTextView (COM) → IWpfTextView (managed)
IWpfTextView view = adapters.GetWpfTextView(vsView);

// ── ITextBuffer ───────────────────────────────────────────────
ITextBuffer   buffer   = view.TextBuffer;
ITextSnapshot snapshot = buffer.CurrentSnapshot; // immutable snapshot

string contentType = buffer.ContentType.TypeName; // "CSharp", "HTML", ...
int    totalChars  = snapshot.Length;
int    totalLines  = snapshot.LineCount;

// Đọc dòng từ snapshot
ITextSnapshotLine line3 = snapshot.GetLineFromLineNumber(2); // 0-based
string lineText         = line3.GetText();
int    lineStart        = line3.Start.Position;   // offset đầu dòng (0-based)
int    lineLength       = line3.Length;           // không tính newline

// Tìm kiếm trong snapshot
int searchFrom = 0;
int foundIdx   = snapshot.GetText().IndexOf("TODO", searchFrom,
                     StringComparison.OrdinalIgnoreCase);

// ── ITextCaret ────────────────────────────────────────────────
ITextCaret caret = view.Caret;

// BufferPosition: SnapshotPoint = offset tuyệt đối (0-based)
SnapshotPoint caretPos = caret.Position.BufferPosition;
int offset0based = caretPos.Position;

// Chuyển đổi offset → line/column (0-based)
ITextSnapshotLine caretLine = caretPos.GetContainingLine();
int lineNumber0 = caretLine.LineNumber;                          // 0-based
int colNumber0  = caretPos.Position - caretLine.Start.Position; // 0-based

// So sánh với DTE (1-based): DTE line = lineNumber0 + 1

// Di chuyển caret
SnapshotPoint newPos = new SnapshotPoint(snapshot, targetOffset);
view.Caret.MoveTo(newPos);

// ── ITextSelection ────────────────────────────────────────────
ITextSelection selection = view.Selection;

bool isEmpty     = selection.IsEmpty;
bool isReversed  = selection.IsReversed; // kéo từ dưới lên trên
var  mode2       = selection.Mode;       // Stream hoặc Box

// SelectedSpans: danh sách SnapshotSpan (multi-caret có thể nhiều spans)
NormalizedSnapshotSpanCollection spans = selection.SelectedSpans;

foreach (SnapshotSpan span in spans)
{
    int start  = span.Start.Position; // 0-based
    int end    = span.End.Position;
    int length = span.Length;
    string text = span.GetText();     // nội dung được chọn

    // Lấy line/col của start và end
    int startLine = span.Start.GetContainingLine().LineNumber;
    int endLine   = span.End.GetContainingLine().LineNumber;
}

// StreamSelectionSpan: logical span từ anchor đến active
var ss = selection.StreamSelectionSpan;
int selStart = ss.Start.Position.Position;
int selEnd   = ss.End.Position.Position;

// Thay đổi selection bằng code
SnapshotSpan newSpan = new SnapshotSpan(snapshot, start: 10, length: 20);
selection.Select(newSpan, isReversed: false);
selection.Clear(); // bỏ chọn

// ── Ghi vào buffer (ITextEdit) ────────────────────────────────
// CreateEdit(): bắt đầu transaction ghi
using (ITextEdit edit = buffer.CreateEdit())
{
    // Insert tại offset
    edit.Insert(position: caretPos.Position, text: "// inserted\n");

    // Delete khoảng
    edit.Delete(start: 0, charsToDelete: 5);

    // Replace khoảng
    edit.Replace(new Span(start: 10, length: 8), "newtext");

    // Apply để commit thay đổi
    // Sau Apply(), snapshot cũ không còn valid
    edit.Apply();
}
```

---

## 4. Document & File APIs

### Document Properties

```csharp
// dte.ActiveDocument: document đang có focus
// dte.Documents: tất cả documents đang mở (kể cả background)
Document doc = dte.ActiveDocument;

string name     = doc.Name;       // tên file, không có path
string fullPath = doc.FullName;   // đường dẫn đầy đủ
string language = doc.Language;   // "CSharp", "Basic", "HTML", "Plain Text"
string kind     = doc.Kind;       // GUID xác định loại document
bool   saved    = doc.Saved;      // false = có thay đổi chưa lưu
bool   readOnly = doc.ReadOnly;
int    encoding = doc.Encoding;   // Windows CodePage (65001 = UTF-8)

// Thao tác
doc.Save();              // lưu file
doc.Save(@"C:\new.cs"); // save as
doc.Close(vsSaveChanges.vsSaveChangesYes); // đóng và lưu
doc.Undo();              // undo thay đổi cuối cùng

// Mở file mới
// vsViewKindCode / vsViewKindTextView / vsViewKindDesigner
dte.ItemOperations.OpenFile(@"C:\file.cs", Constants.vsViewKindCode);
dte.ItemOperations.Navigate("https://docs.microsoft.com");

// ProjectItem liên kết với project
if (doc.ProjectItem != null)
{
    string projName  = doc.ProjectItem.ContainingProject.Name;
    int    fileCount = doc.ProjectItem.FileCount; // thường = 1
    string filePath  = doc.ProjectItem.FileNames[1]; // 1-based index
}

// Duyệt tất cả documents đang mở
foreach (Document d in dte.Documents)
{
    string mark = d.Saved ? "✓" : "*";
    Console.WriteLine($"[{mark}] {d.Language,-12} {d.Name}");
}
```

### TextDocument & EditPoint

```csharp
// doc.Object("TextDocument"): lấy TextDocument cho text-based files
// Trả về null (hoặc throw) với designer, binary...
var textDoc = (TextDocument)doc.Object("TextDocument");

// TextPoint (read-only positions)
// Line, LineCharOffset: 1-based
// AbsoluteCharOffset: offset từ đầu file (1-based)
int firstLine = textDoc.StartPoint.Line;          // = 1
int lastLine  = textDoc.EndPoint.Line;            // = tổng số dòng
int totalChar = textDoc.EndPoint.AbsoluteCharOffset
              - textDoc.StartPoint.AbsoluteCharOffset;

// EditPoint — mutable cursor để đọc/ghi
// CreateEditPoint() tạo EditPoint tại vị trí TextPoint đó
EditPoint ep = textDoc.StartPoint.CreateEditPoint();

// Đọc nội dung
string first200 = ep.GetText(200);              // đọc 200 ký tự
string lines13  = ep.GetLines(1, 4);            // dòng 1, 2, 3 (endLine exclusive)

// Di chuyển
ep.MoveToLineAndOffset(line: 10, offset: 1);   // 1-based
ep.StartOfDocument();                           // về đầu file
ep.EndOfDocument();                             // về cuối file
ep.LineDown(Count: 5);                          // xuống 5 dòng

// Ghi nội dung (THAY ĐỔI FILE THẬT)
ep.Insert("// comment\n");                         // chèn tại ep
ep.MoveToLineAndOffset(1, 1);
EditPoint endPoint = textDoc.EndPoint.CreateEditPoint();
ep.ReplaceText(endPoint, "new content",             // replace toàn bộ
    (int)vsEPReplaceTextOptions.vsEPReplaceTextAutoformat);

// Sau khi ghi, nên lưu:
doc.Save();
```

### ExecuteCommand — Gọi built-in VS commands

```csharp
// Gọi command bằng tên chuỗi
// Arg tùy chọn (không phải command nào cũng cần)
dte.ExecuteCommand("Edit.FormatDocument");
dte.ExecuteCommand("Edit.FormatSelection");
dte.ExecuteCommand("Edit.GoToLine", "42");     // nhảy đến dòng 42
dte.ExecuteCommand("File.SaveAll");
dte.ExecuteCommand("Build.BuildSolution");
dte.ExecuteCommand("View.Output");
dte.ExecuteCommand("View.SolutionExplorer");
dte.ExecuteCommand("Edit.FindAndReplace");
```

---

## 5. Project & Solution APIs

### Solution

```csharp
Solution sol = dte.Solution;

string path     = sol.FullName;         // đường dẫn file .sln
bool   isOpen   = sol.IsOpen;
bool   isDirty  = sol.IsDirty;          // thay đổi chưa lưu
int    projCount = sol.Projects.Count;

// Mở / tạo / lưu solution
sol.Open(@"C:\MySolution\My.sln");
sol.Create(@"C:\NewSolution", "MySolution");
sol.SaveAs(@"C:\Backup\My.sln");
sol.Close(SaveFirst: true);

// SolutionBuild
SolutionBuild sb = sol.SolutionBuild;
string activeConfig = sb.ActiveConfiguration.Name; // "Debug" hoặc "Release"
sb.Build(WaitForBuildToFinish: true);
sb.Rebuild();
sb.Clean();
// LastBuildInfo: số project FAILED (0 = tất cả thành công)
bool allSuccess = sb.LastBuildInfo == 0;
```

### Project & Properties

```csharp
// Duyệt tất cả projects trong solution
foreach (Project proj in dte.Solution.Projects)
{
    string name       = proj.Name;
    string uniqueName = proj.UniqueName;  // đường dẫn tương đối từ .sln
    string fullName   = proj.FullName;    // đường dẫn đầy đủ đến .csproj
    string kind       = proj.Kind;        // GUID loại project
    // C# = {FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}
    // SolutionFolder = {66A26720-8FB5-11D2-AA7E-00C04F688DDE}

    // Project Properties — truy cập theo tên key
    Property assemblyName = proj.Properties.Item("AssemblyName");
    Property rootNs       = proj.Properties.Item("RootNamespace");
    Property outputType   = proj.Properties.Item("OutputType");
    // OutputType: 0=exe, 1=winexe, 2=library
    Property targetFw     = proj.Properties.Item("TargetFrameworkMoniker");
    // → ".NETFramework,Version=v4.6"

    // Build / open / save project
    proj.Save();
    dte.Solution.SolutionBuild.BuildProject(
        SolutionConfiguration: "Debug|AnyCPU",
        ProjectUniqueName: proj.UniqueName,
        WaitForBuildToFinish: true);
}
```

### VSProject — References (C#/VB projects)

```csharp
using VSLangProj;

// proj.Object: underlying COM object
// Cast sang VSProject cho C#/VB
VSProject vsProj = proj.Object as VSProject;

// Duyệt references
foreach (Reference r in vsProj.References)
{
    string name    = r.Name;          // tên assembly
    string path    = r.Path;          // đường dẫn DLL
    string version = $"{r.MajorVersion}.{r.MinorVersion}";
    var    type    = r.Type;          // prjReferenceType.prjReferenceTypeAssembly / .Project
}

// Thêm reference
vsProj.References.Add(@"C:\path\to\MyLib.dll");  // từ đường dẫn
// Reference đến project khác trong solution
vsProj.References.AddProject(otherProject);

// Xóa reference
Reference refToRemove = vsProj.References.Item("MyLib");
refToRemove.Remove();
```

### ConfigurationManager

```csharp
ConfigurationManager cm = proj.ConfigurationManager;

// ActiveConfiguration: "Debug|AnyCPU", "Release|x64" ...
Configuration active = cm.ActiveConfiguration;
string configName = active.ConfigurationName;   // "Debug"
string platform   = active.PlatformName;        // "AnyCPU"
bool   buildable  = active.IsBuildable;

// Properties của configuration
string outputPath = active.Properties.Item("OutputPath").Value as string;
// → "bin\Debug\"
bool   optimize   = (bool)active.Properties.Item("Optimize").Value;
string defines    = active.Properties.Item("DefineConstants").Value as string;
```

### ProjectItems — Duyệt cây file

```csharp
// Đệ quy duyệt tất cả files trong project
void DumpItems(ProjectItems items, int depth = 0)
{
    if (items == null) return;
    foreach (ProjectItem item in items)
    {
        string indent = new string(' ', depth * 2);

        // FileNames[1]: đường dẫn file (index 1-based)
        string path = item.FileNames[1];

        // BuildAction: 0=None, 1=Compile, 2=Content, 3=EmbeddedResource
        string buildAction = item.Properties?.Item("BuildAction")?.Value?.ToString();

        // Kind: vsProjectItemKindPhysicalFolder hoặc vsProjectItemKindPhysicalFile
        bool isFolder = item.Kind == Constants.vsProjectItemKindPhysicalFolder;

        Console.WriteLine($"{indent}{(isFolder ? "[DIR]" : "[FILE]")} {item.Name}");

        // Đệ quy vào sub-items
        DumpItems(item.ProjectItems, depth + 1);
    }
}

DumpItems(proj.ProjectItems);

// Thêm / xóa file
ProjectItem newItem = proj.ProjectItems.AddFromFile(@"C:\NewFile.cs");
ProjectItem existing = proj.ProjectItems.Item("Program.cs");
existing.Remove();   // xóa khỏi project (giữ file)
existing.Delete();   // xóa file luôn
```

---

## 6. Menu & Command

### VSCommandTable.vsct

```xml
<?xml version="1.0" encoding="utf-8"?>
<CommandTable xmlns="http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable"
              xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <Extern href="stdidcmd.h"/>
  <Extern href="vsshlids.h"/>

  <Commands package="guidMyPackage">

    <!-- Submenu dưới Tools menu -->
    <Menus>
      <Menu guid="guidMyCmdSet" id="SubMenu" priority="0x0100" type="Menu">
        <Parent guid="guidMyCmdSet" id="MyMenuGroup"/>
        <Strings>
          <MenuText>My Extension</MenuText>
        </Strings>
      </Menu>
    </Menus>

    <Groups>
      <!-- Group trong Tools menu -->
      <Group guid="guidMyCmdSet" id="MyMenuGroup" priority="0x0600">
        <Parent guid="guidSHLMainMenu" id="IDM_VS_MENU_TOOLS"/>
      </Group>
      <!-- Group trong submenu -->
      <Group guid="guidMyCmdSet" id="SubMenuGroup" priority="0x0000">
        <Parent guid="guidMyCmdSet" id="SubMenu"/>
      </Group>
    </Groups>

    <Buttons>
      <!-- Menu item / button -->
      <Button guid="guidMyCmdSet" id="cmdidMyCommand" priority="0x0100" type="Button">
        <Parent guid="guidMyCmdSet" id="SubMenuGroup"/>
        <Strings>
          <ButtonText>My Command</ButtonText>
          <ToolTipText>Does something useful</ToolTipText>
          <!-- CommandName: dùng để bind keyboard shortcut qua dte.Commands -->
          <CommandName>MyExtension.MyCommand</CommandName>
        </Strings>
      </Button>
    </Buttons>

  </Commands>

  <!-- Keyboard shortcuts -->
  <KeyBindings>
    <!-- Ctrl+Shift+1 trong mọi context -->
    <KeyBinding guid="guidMyCmdSet" id="cmdidMyCommand"
                editor="guidVSStd97" key1="1" mod1="Control Shift"/>
  </KeyBindings>

  <Symbols>
    <GuidSymbol name="guidMyPackage"  value="{YOUR-PACKAGE-GUID}" />
    <GuidSymbol name="guidMyCmdSet"   value="{YOUR-CMDSET-GUID}">
      <IDSymbol name="MyMenuGroup"    value="0x1010"/>
      <IDSymbol name="SubMenuGroup"   value="0x1020"/>
      <IDSymbol name="SubMenu"        value="0x2000"/>
      <IDSymbol name="cmdidMyCommand" value="0x0101"/>
    </GuidSymbol>
  </Symbols>
</CommandTable>
```

### OleMenuCommand — Đăng ký và xử lý

```csharp
internal sealed class MyCommand
{
    private readonly AsyncPackage _package;

    private MyCommand(AsyncPackage package, OleMenuCommandService cs)
    {
        _package = package;

        // CommandID = (CommandSet GUID, integer ID) — phải khớp với .vsct
        var cmdId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.cmdidMyCommand);

        // OleMenuCommand: mạnh hơn MenuCommand
        //   • Có BeforeQueryStatus để cập nhật Enabled/Visible/Text động
        //   • MenuCommand đơn giản hơn nhưng không có BeforeQueryStatus
        var cmd = new OleMenuCommand(OnExecute, cmdId);
        cmd.BeforeQueryStatus += OnBeforeQueryStatus;
        cs.AddCommand(cmd);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var cs = await package.GetServiceAsync(typeof(IMenuCommandService))
                 as OleMenuCommandService;
        if (cs != null)
            new MyCommand(package, cs);
    }

    // Được VS gọi ngay TRƯỚC KHI render menu item
    // Dùng để: enable/disable, show/hide, thay đổi text theo context
    private void OnBeforeQueryStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var cmd = (OleMenuCommand)sender;
        var dte = _package.GetService<DTE2, SDTE>();

        bool hasDoc = dte?.ActiveDocument != null;

        cmd.Enabled = hasDoc;      // true = có thể click
        cmd.Visible = true;        // true = hiển thị trong menu
        cmd.Checked = false;       // true = hiện dấu ✓ bên trái

        // Text động
        cmd.Text = hasDoc
            ? $"My Command ({dte.ActiveDocument.Name})"
            : "My Command (no document)";
    }

    // Được gọi khi user click menu item hoặc nhấn shortcut
    private void OnExecute(object sender, EventArgs e)
    {
        // RunAsync: chạy async từ sync context an toàn (không deadlock)
        _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            // ... thực hiện logic
        });
    }
}
```

### Context Menu (click chuột phải)

```xml
<!-- Trong .vsct: đặt button vào context menu của code editor -->
<Button guid="guidMyCmdSet" id="cmdidContextCmd" priority="0x0100" type="Button">
  <!-- IDM_VS_CTXT_CODEWIN = context menu của code editor -->
  <Parent guid="guidSHLMainMenu" id="IDM_VS_CTXT_CODEWIN"/>
  <Strings>
    <ButtonText>My Context Action</ButtonText>
  </Strings>
</Button>

<!-- IDM_VS_CTXT_PROJNODE = context menu của project node trong Solution Explorer -->
<!-- IDM_VS_CTXT_ITEMNODE = context menu của file node -->
```

---

## 7. Toolbar & CommandBar

```csharp
// dte.CommandBars: collection tất cả toolbars/menubars
var cmdBars = (CommandBars)dte.CommandBars;

// Truy cập toolbar theo tên
CommandBar toolbar = cmdBars["Standard"];

// Tạo toolbar mới
CommandBar newBar = cmdBars.Add("My Toolbar",
    MsoBarPosition.msoBarTop,  // vị trí: Top/Bottom/Left/Right/Floating
    MenuBar: false,
    Temporary: false);         // false = lưu lại sau khi đóng VS

// Thêm button vào toolbar
CommandBarButton btn = (CommandBarButton)newBar.Controls.Add(
    Type: MsoControlType.msoControlButton,
    Temporary: false);

btn.Caption     = "My Button";
btn.TooltipText = "Click để làm gì đó";
btn.FaceId      = 2;           // icon ID (từ Office icon set)
btn.Style       = MsoButtonStyle.msoButtonIconAndCaption;
btn.Enabled     = true;
btn.Visible     = true;

// Xử lý click
btn.Click += (CommandBarButton ctrl, ref bool cancelDefault) =>
{
    System.Windows.MessageBox.Show("Button clicked!");
};
```

---

## 8. Tool Window

### Định nghĩa Tool Window

```csharp
// Thuộc tính đăng ký Tool Window với VS
// Style: window style (Float, Tabbed, MDI, ...)
// Window: GUID của tool window dùng để dock bên cạnh
[ProvideToolWindow(typeof(MyToolWindow),
    Style   = VsDockStyle.Tabbed,
    Window  = "3ae79031-e1bc-11d0-8f78-00a0c9110057")] // Solution Explorer GUID
public sealed class MyPackage : AsyncPackage { ... }

// Tool Window class
public class MyToolWindow : ToolWindowPane
{
    public MyToolWindow() : base(null)
    {
        // Caption hiển thị trên tab
        Caption = "My Tool Window";
        // Content là WPF UserControl
        Content = new MyToolWindowControl();
    }
}

// WPF UserControl
public partial class MyToolWindowControl : UserControl
{
    public MyToolWindowControl() { InitializeComponent(); }
}
```

### Mở / Đóng Tool Window

```csharp
// Mở Tool Window
ToolWindowPane window = await package.ShowToolWindowAsync(
    toolWindowType : typeof(MyToolWindow),
    id             : 0,                      // instance ID (0 cho single instance)
    create         : true,                   // tạo mới nếu chưa có
    cancellationToken: cancellationToken);

// Lấy IVsWindowFrame để control window
IVsWindowFrame frame = (IVsWindowFrame)window.Frame;
frame.Show();        // hiện window
frame.Hide();        // ẩn window
frame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);

// Lấy window đã tồn tại (không tạo mới)
ToolWindowPane existing = package.FindToolWindow(
    toolWindowType: typeof(MyToolWindow),
    id:     0,
    create: false);
```

---

## 9. Events

> **Quan trọng về Lifetime:** Phải giữ strong reference đến event objects ở field của class. Nếu để local variable, GC sẽ collect và events không còn fire nữa!

```csharp
// dte.Events: container tất cả event objects
Events2 events = (Events2)dte.Events;

// PHẢI lưu vào FIELDS, không được để local variable!
private BuildEvents     _buildEvents;
private SolutionEvents  _solutionEvents;
private DocumentEvents  _documentEvents;
private WindowEvents    _windowEvents;
private SelectionEvents _selectionEvents;
private DebuggerEvents  _debuggerEvents;
```

### BuildEvents

```csharp
_buildEvents = events.BuildEvents;

// scope: vsBuildScopeSolution / vsBuildScopeProject / vsBuildScopeBatch
// action: vsBuildActionBuild / vsBuildActionRebuildAll / vsBuildActionClean
_buildEvents.OnBuildBegin += (vsBuildScope scope, vsBuildAction action) =>
{
    Console.WriteLine($"Build started: scope={scope}, action={action}");
};

_buildEvents.OnBuildDone += (vsBuildScope scope, vsBuildAction action) =>
{
    // LastBuildInfo = 0 → tất cả success; > 0 → số project fail
    int failures = dte.Solution.SolutionBuild.LastBuildInfo;
    Console.WriteLine($"Build done. Failures: {failures}");
};

_buildEvents.OnBuildProjConfigBegin +=
    (string project, string projConfig, string platform, string solutionConfig) =>
{
    Console.WriteLine($"Building: {project} [{projConfig}|{platform}]");
};

_buildEvents.OnBuildProjConfigDone +=
    (string project, string projConfig, string platform, string solutionConfig, bool success) =>
{
    Console.WriteLine($"{project}: {(success ? "SUCCESS" : "FAILED")}");
};
```

### SolutionEvents

```csharp
_solutionEvents = events.SolutionEvents;

_solutionEvents.Opened         += () => Console.WriteLine($"Solution opened: {dte.Solution.FullName}");
_solutionEvents.BeforeClosing  += () => Console.WriteLine("Solution closing...");
_solutionEvents.AfterClosing   += () => Console.WriteLine("Solution closed");
_solutionEvents.ProjectAdded   += (Project p) => Console.WriteLine($"Project added: {p.Name}");
_solutionEvents.ProjectRemoved += (Project p) => Console.WriteLine($"Project removed: {p.Name}");
_solutionEvents.ProjectRenamed += (Project p, string oldName)
    => Console.WriteLine($"Project renamed: {oldName} → {p.Name}");
```

### DocumentEvents

```csharp
// null = monitor tất cả documents (không lọc theo document cụ thể)
// Có thể truyền vào Document object để chỉ monitor document đó
_documentEvents = events.get_DocumentEvents(null);

_documentEvents.DocumentOpened  += (Document doc)
    => Console.WriteLine($"Opened: {doc.Name}");
_documentEvents.DocumentClosing += (Document doc)
    => Console.WriteLine($"Closing: {doc.Name} (saved={doc.Saved})");
_documentEvents.DocumentSaved   += (Document doc)
    => Console.WriteLine($"Saved: {doc.FullName}");
```

### WindowEvents

```csharp
// null = monitor tất cả windows
_windowEvents = events.get_WindowEvents(null);

// gotFocus: window nhận focus; lostFocus: window mất focus (có thể null)
_windowEvents.WindowActivated += (Window gotFocus, Window lostFocus)
    => Console.WriteLine($"Activated: {gotFocus?.Caption}");
_windowEvents.WindowCreated   += (Window win)
    => Console.WriteLine($"Created: {win?.Caption}");
_windowEvents.WindowClosing   += (Window win)
    => Console.WriteLine($"Closing: {win?.Caption}");
```

### SelectionEvents

```csharp
// Fire mỗi khi caret di chuyển — rất nhiều events!
// Nên giới hạn logic bên trong hoặc throttle
_selectionEvents = events.SelectionEvents;
_selectionEvents.OnChange += () =>
{
    ThreadHelper.ThrowIfNotOnUIThread();
    var sel = dte.ActiveDocument?.Selection as TextSelection;
    if (sel != null)
        Console.WriteLine($"Caret: Line={sel.CurrentLine}, Col={sel.CurrentColumn}");
};
```

### DebuggerEvents

```csharp
_debuggerEvents = events.DebuggerEvents;

// reason: dbgEventReasonBreakpoint, dbgEventReasonStep, dbgEventReasonException...
// executionAction: có thể thay đổi để skip breakpoint, v.v.
_debuggerEvents.OnEnterBreakMode +=
    (dbgEventReason reason, ref dbgExecutionAction executionAction) =>
{
    Console.WriteLine($"Breakpoint hit: reason={reason}");
};

_debuggerEvents.OnEnterRunMode    += (dbgEventReason reason)
    => Console.WriteLine("Debugger resumed");
_debuggerEvents.OnEnterDesignMode += (dbgEventReason reason)
    => Console.WriteLine("Debug stopped");
```

### Hủy đăng ký (cleanup)

```csharp
// Luôn hủy đăng ký khi không cần nữa (trong Dispose hoặc khi toggle OFF)
if (_buildEvents != null)
{
    _buildEvents.OnBuildBegin -= OnBuildBegin;
    _buildEvents.OnBuildDone  -= OnBuildDone;
    _buildEvents = null;  // release reference để GC collect
}
```

---

## 10. Settings / Options Page

### Định nghĩa Options Page

```csharp
// ProvideOptionPage: đăng ký page trong Tools > Options
// Category: nhóm (hiện ở cây bên trái)
// Page: tên trang
[ProvideOptionPage(typeof(MyOptionsPage),
    category: "My Extension",
    pageName: "General",
    categoryResourceID: 0,
    pageNameResourceID: 0,
    supportsAutomation: true)]
public sealed class MyPackage : AsyncPackage { ... }

// Options Page class
public class MyOptionsPage : DialogPage
{
    // Properties tự động serialize/deserialize vào VS registry
    [Category("Connection")]
    [DisplayName("Server URL")]
    [Description("URL của server backend")]
    public string ServerUrl { get; set; } = "https://api.example.com";

    [Category("Behavior")]
    [DisplayName("Auto Format")]
    [Description("Tự động format code khi save")]
    public bool AutoFormat { get; set; } = true;

    [Category("Behavior")]
    [DisplayName("Max Items")]
    [Description("Số lượng item tối đa")]
    public int MaxItems { get; set; } = 100;
}
```

### Đọc Settings từ code

```csharp
// GetDialogPage<T>(): lấy instance của options page
// Giá trị được VS persist tự động vào registry
var options = (MyOptionsPage)package.GetDialogPage(typeof(MyOptionsPage));
string url       = options.ServerUrl;
bool   autoFmt   = options.AutoFormat;
int    maxItems  = options.MaxItems;

// Mở trang settings trong UI
dte.ExecuteCommand("Tools.Options", "My Extension.General");
```

---

## 11. IntelliSense / Completion

### Custom Completion Source (MEF)

```csharp
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;

// ICompletionSourceProvider: factory tạo ICompletionSource
// Export MEF với ContentType = "csharp" và Name
[Export(typeof(ICompletionSourceProvider))]
[ContentType("csharp")]           // áp dụng cho C# files
[Name("MyCompletionProvider")]
[Order(Before = "default")]       // ưu tiên cao hơn default provider
internal class MyCompletionSourceProvider : ICompletionSourceProvider
{
    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        => new MyCompletionSource(textBuffer);
}

// ICompletionSource: cung cấp danh sách completion items
internal class MyCompletionSource : ICompletionSource
{
    private readonly ITextBuffer _buffer;
    private bool _disposed;

    public MyCompletionSource(ITextBuffer buffer) { _buffer = buffer; }

    // AugmentCompletionSession: được gọi khi VS hiện completion list
    // completionSets: thêm CompletionSet vào đây
    public void AugmentCompletionSession(
        ICompletionSession session,
        IList<CompletionSet> completionSets)
    {
        if (_disposed) return;

        // Tạo danh sách completions
        var completions = new List<Completion>
        {
            // Completion(displayText, insertionText, description, icon, iconKey)
            new Completion("MyKeyword", "MyKeyword",
                "My custom keyword", null, null),
            new Completion("MyMethod()", "MyMethod()",
                "Custom method snippet", null, null),
        };

        // Tính tracking span (vùng text sẽ bị replace khi chọn completion)
        ITrackingSpan applicableTo = FindTokenSpanAtPosition(session);

        completionSets.Add(new CompletionSet(
            moniker:     "MyCompletions",    // unique ID
            displayName: "My Extension",     // tên hiện trên tab
            applicableTo: applicableTo,
            completions: completions,
            completionBuilders: null));
    }

    private ITrackingSpan FindTokenSpanAtPosition(ICompletionSession session)
    {
        SnapshotPoint currentPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot)
                                     ?? _buffer.CurrentSnapshot.GetEnd();
        // Tạo tracking span bắt đầu từ currentPoint
        return _buffer.CurrentSnapshot.CreateTrackingSpan(
            currentPoint.Position, 0, SpanTrackingMode.EdgeInclusive);
    }

    public void Dispose() { _disposed = true; }
}
```

### FileCodeModel — Duyệt code structure

```csharp
// ProjectItem.FileCodeModel: code model của file
FileCodeModel codeModel = doc.ProjectItem?.FileCodeModel;
if (codeModel == null) return; // binary, designer, ...

// Duyệt tất cả code elements ở top level
foreach (CodeElement element in codeModel.CodeElements)
{
    // vsCMElement: vsCMElementNamespace, vsCMElementClass,
    //              vsCMElementFunction, vsCMElementProperty, ...
    if (element.Kind == vsCMElement.vsCMElementNamespace)
    {
        var ns = (CodeNamespace)element;
        Console.WriteLine($"Namespace: {ns.FullName}");

        // Duyệt members của namespace
        foreach (CodeElement member in ns.Members)
        {
            if (member.Kind == vsCMElement.vsCMElementClass)
            {
                var cls = (CodeClass)member;
                Console.WriteLine($"  Class: {cls.Name}");

                // Duyệt members của class
                foreach (CodeElement m in cls.Members)
                {
                    if (m.Kind == vsCMElement.vsCMElementFunction)
                    {
                        var func = (CodeFunction)m;
                        Console.WriteLine($"    Method: {func.Name}({func.Parameters.Count} params)");
                    }
                }
            }
        }
    }
}

// Thêm class / method (thay đổi file thật)
CodeNamespace myNs   = (CodeNamespace)codeModel.CodeElements.Item(1);
CodeClass     newCls = myNs.AddClass("MyClass", Position: -1,
    Bases: null, ImplementedInterfaces: null,
    Access: vsCMAccess.vsCMAccessPublic);

newCls.AddFunction("MyMethod", vsCMFunction.vsCMFunctionFunction,
    Type: "void", Position: -1,
    Access: vsCMAccess.vsCMAccessPublic);
```

---

## 12. Text Adornment & Tagger

### Custom Tagger (highlight text)

```csharp
// ITaggerProvider: factory cho ITagger
// Export MEF với ContentType và TagType
[Export(typeof(ITaggerProvider))]
[ContentType("text")]
[TagType(typeof(TextMarkerTag))]
internal class TodoTaggerProvider : ITaggerProvider
{
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        => new TodoTagger(buffer) as ITagger<T>;
}

// ITagger<TextMarkerTag>: tạo highlights cho text patterns
internal class TodoTagger : ITagger<TextMarkerTag>
{
    private readonly ITextBuffer _buffer;

    public TodoTagger(ITextBuffer buffer)
    {
        _buffer = buffer;
        // Fire TagsChanged khi buffer thay đổi
        _buffer.Changed += (s, e) => TagsChanged?.Invoke(this,
            new SnapshotSpanEventArgs(
                new SnapshotSpan(_buffer.CurrentSnapshot, 0,
                                 _buffer.CurrentSnapshot.Length)));
    }

    // GetTags: trả về tags cho các spans được yêu cầu
    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(
        NormalizedSnapshotSpanCollection spans)
    {
        ITextSnapshot snapshot = _buffer.CurrentSnapshot;
        string text = snapshot.GetText();

        // Tìm tất cả "TODO" trong file
        int start = 0;
        while (true)
        {
            int idx = text.IndexOf("TODO", start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;

            var span = new SnapshotSpan(snapshot, idx, 4);

            // "MarkerFormatDefinition/HighlightedReference" = màu highlight mặc định
            // Có thể tạo custom MarkerFormatDefinition
            yield return new TagSpan<TextMarkerTag>(span,
                new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));

            start = idx + 4;
        }
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
}
```

---

## 13. NuGet References bắt buộc

| Package | Version | Namespace | Cung cấp |
|---------|---------|-----------|----------|
| `Microsoft.VSSDK.BuildTools` | 15.9.3084 | — | VSCT compiler, VSIX packager |
| `EnvDTE` | 8.0.2 | `EnvDTE` | DTE, Document, TextSelection, Project |
| `EnvDTE80` | 8.0.3 | `EnvDTE80` | DTE2, Solution2 |
| `Microsoft.VisualStudio.OLE.Interop` | 7.10.6040 | `...OLE.Interop` | IOleCommandTarget |
| `Microsoft.VisualStudio.Shell.Interop` | 7.10.6040 | `...Shell.Interop` | IVsOutputWindow, IVsStatusbar |
| `Microsoft.VisualStudio.TextManager.Interop` | 7.10.6040 | `...TextManager.Interop` | IVsTextManager, IVsTextView |
| `VSLangProj` | 7.0.6000 | `VSLangProj` | VSProject, Reference |

**Managed assemblies** (resolve qua `AssemblySearchPaths`, không cần HintPath):

| Assembly | Namespace | Cung cấp |
|----------|-----------|----------|
| `Microsoft.VisualStudio.Shell.15.0` | `...Shell` | AsyncPackage, ThreadHelper |
| `Microsoft.VisualStudio.Shell.Framework` | `...Shell` | OleMenuCommand, DialogPage |
| `Microsoft.VisualStudio.ComponentModelHost` | `...ComponentModelHost` | IComponentModel (MEF) |
| `Microsoft.VisualStudio.Editor` | `...Editor` | IVsEditorAdaptersFactoryService |
| `Microsoft.VisualStudio.Text.Data` | `...Text` | ITextBuffer, ITextSnapshot, SnapshotSpan |
| `Microsoft.VisualStudio.Text.Logic` | `...Text.Tagging` | ITextEdit, ITagger |
| `Microsoft.VisualStudio.Text.UI` | `...Text.Editor` | ITextView, ITextSelection, ITextCaret |
| `Microsoft.VisualStudio.Text.UI.Wpf` | `...Text.Editor` | IWpfTextView, IAdornmentLayer |

### Thread Safety — Cheat Sheet

```csharp
// ✅ ĐÚNG: Switch trước khi dùng VS API
await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
var dte = package.GetService(typeof(DTE)) as DTE2;

// ✅ ĐÚNG: Kiểm tra UI thread (throw nếu không đúng)
ThreadHelper.ThrowIfNotOnUIThread();

// ✅ ĐÚNG: Chạy async từ sync context
package.JoinableTaskFactory.RunAsync(async () =>
{
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
    // ... code cần UI thread
});

// ✅ ĐÚNG: GetServiceAsync (không block)
var svc = await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;

// ❌ SAI: GetService() trực tiếp trên background thread
var svc = package.GetService(typeof(SVsOutputWindow)); // có thể deadlock!

// ❌ SAI: Gọi VS API không có SwitchToMainThreadAsync
// Sẽ throw InvalidOperationException ở VS2017+
```

---

*Tài liệu được tổng hợp từ session thực hành VS2017 VSIX Extension Development.*
