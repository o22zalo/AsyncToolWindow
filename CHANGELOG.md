# Changelog

All notable changes to **AsyncToolWindowSample** are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased] – 2026-03-19 (patch: CS0122-getservice)

### Fixed

#### `src/Services/SelectionService.cs`
- **CS0122 (×2) – `AsyncPackage.GetService(Type)` inaccessible:**
  `AsyncPackage.GetService()` là `protected internal` — không thể gọi từ class bên ngoài.
  **Fix:** Lưu `AsyncPackage` vào `IServiceProvider _serviceProvider` (interface public mà
  `AsyncPackage` implement). Thay tất cả `_package.GetService(...)` bằng
  `_serviceProvider.GetService(...)` ở dòng 69 (DTE) và dòng 164 (SVsTextManager).
  `GetServiceAsync()` trong `InitializeAsync()` vẫn dùng `_package` bình thường
  vì đó là method `public` của `AsyncPackage`.

---



### Added

#### `src/Services/SelectionService.cs` *(new file)*
- **Mục đích:** Tách biệt logic truy cập Selection/Caret của VS thành service riêng,
  hỗ trợ cả hai tầng API:
  - **Tier 1 – DTE `TextSelection` (COM):** đơn giản, không cần MEF, phù hợp thao tác
    điều hướng, tìm kiếm, chọn vùng văn bản theo dòng/offset 1-based.
  - **Tier 2 – `IWpfTextView` (MEF):** chính xác, managed, hỗ trợ multi-caret, đọc/ghi
    buffer qua `ITextEdit` transaction, offset 0-based.
- **DTOs (data transfer objects) bổ sung:**
  - `DteCaretInfo` – snapshot caret 1-based từ DTE.
  - `MefCaretInfo` – snapshot caret 0-based từ MEF.
  - `SelectionSpanInfo` – thông tin một `SnapshotSpan` đã chọn.
- **Public API:**
  - `InitializeAsync()` – resolve MEF `IVsEditorAdaptersFactoryService` qua
    `SComponentModel`; safe to call on background thread.
  - **Tier 1:** `GetDteSelection()`, `GetDteCaretInfo()`, `GotoLine()`,
    `SelectAll()`, `SelectCurrentLine()`, `CollapseSelection()`, `FindText()`.
  - **Tier 2:** `GetActiveWpfTextView()`, `GetMefCaretInfo()`,
    `GetMefSelectedSpans()`, `InsertAtCaret()`, `ReplaceSelection()`,
    `GetBufferText()`.
- **Lý do:** Mở rộng demo extension theo tài liệu VS2017 Extension API Reference,
  phần 3 "Selection APIs".

### Changed

#### `src/ToolWindows/SampleToolWindowState.cs`
- **Thêm** property `SelectionService Selection`.
- **Lý do:** Truyền service xuống Tool Window Control theo cùng pattern với
  `OutputWindowService` và `StatusBarService`.

#### `src/MyPackage.cs`
- **Thêm** property `SelectionService Selection` (singleton).
- **`InitializeAsync`:** khởi tạo `SelectionService` và gọi `InitializeAsync()`
  song song với hai service hiện có.
- **`InitializeToolWindowAsync`:** populate `SampleToolWindowState.Selection`.
- **Lý do:** Package là nơi duy nhất resolve VS services – tránh service locator
  rải rác.

#### `src/ToolWindows/SampleToolWindowControl.xaml`
- **Thêm** 9 button mới chia thành hai nhóm:
  - *Selection (DTE / Tier 1):*
    - "Show Caret Info (DTE)" – in Line/Col/Anchor/Active/Mode vào Output.
    - "Select Current Line (DTE)" – `SelectLine()`.
    - "Find 'TODO' (DTE)" – `FindText("TODO")`, báo found/not found.
    - "Collapse Selection (DTE)" – `Collapse()`.
  - *Selection (MEF / Tier 2):*
    - "Show Caret Info (MEF)" – in Offset/Line/Col/TotalChars/ContentType.
    - "Show Selected Spans (MEF)" – in tất cả `SnapshotSpan` đang chọn.
    - "Insert Text at Caret (MEF)" – chèn comment placeholder qua `ITextEdit`.
    - "Replace Selection (MEF)" – wrap selection trong comment qua `ITextEdit`.
    - "Buffer Char Count (MEF)" – đếm chars và lines của buffer hiện tại.
- **Thêm** `ScrollViewer` bao ngoài `StackPanel` để Tool Window có thể scroll khi
  danh sách button dài hơn chiều cao cửa sổ.
- **`d:DesignHeight`** tăng từ 450 → 720 để khớp layout mới.
- **Lý do:** Cần UI demo trực tiếp trong Tool Window theo đúng mục tiêu extension.

#### `src/ToolWindows/SampleToolWindowControl.xaml.cs`
- **Thêm** property `Selection => _state.Selection`.
- **Thêm** 9 handler tương ứng 9 button mới:
  - `Button_DteCaretInfo_Click` – hiện `DteCaretInfo` vào Output Window.
  - `Button_DteSelectLine_Click` – gọi `SelectCurrentLine()`.
  - `Button_DteFindTodo_Click` – gọi `FindText("TODO")`.
  - `Button_DteCollapse_Click` – gọi `CollapseSelection()`.
  - `Button_MefCaretInfo_Click` – hiện `MefCaretInfo` vào Output Window.
  - `Button_MefSelectedSpans_Click` – hiện tất cả spans vào Output Window.
  - `Button_MefInsert_Click` – gọi `InsertAtCaret()`.
  - `Button_MefReplace_Click` – gọi `ReplaceSelection()`.
  - `Button_MefBufferCount_Click` – gọi `GetBufferText()` và đếm.
- **Thêm** helper `Truncate(string, int)` để tránh log quá dài.
- **Lý do:** Demo đầy đủ hai tầng Selection API theo tài liệu, không block UI thread.

#### `src/AsyncToolWindowSample.csproj`
- **Thêm** `<Compile Include="Services\SelectionService.cs" />`.
- **Thêm** 4 `<Reference>` cho các managed assemblies cần thiết cho Tier 2:
  - `Microsoft.VisualStudio.ComponentModelHost` (MEF container)
  - `Microsoft.VisualStudio.Editor` (`IVsEditorAdaptersFactoryService`)
  - `Microsoft.VisualStudio.Text.Logic`
  - `Microsoft.VisualStudio.Text.UI` / `Microsoft.VisualStudio.Text.UI.Wpf`
    (`IWpfTextView`, `ITextSelection`, `ITextCaret`)
- **Lý do:** Compiler cần resolve các interface MEF từ VS managed assemblies;
  chúng không có HintPath vì resolve qua `AssemblySearchPaths` của VS.

---

## [Unreleased] – 2026-03-19 (patch: compiler-errors)

### Fixed

#### `src/Services/StatusBarService.cs`
- **CS0165 – Use of unassigned local variable 'frozen':**
  Đổi `_statusBar?.IsFrozen(out int frozen)` thành `int frozen = 0; _statusBar?.IsFrozen(out frozen)`.
- **CS1503 (×2) – ref ulong → ref uint:**
  `IVsStatusbar.Progress` nhận `ref uint`; đổi `ReportProgress`/`ClearProgress` sang `ref uint cookie`.

#### `src/ToolWindows/SampleToolWindowControl.xaml.cs`
- Đổi `ulong cookie = 0` → `uint cookie = 0` trong `Button_Progress_Click`.

---

## [Unreleased] – 2026-03-19 (feat: output-window-status-bar)

### Added

#### `src/Services/OutputWindowService.cs` *(new file)*
- `InitializeAsync()`, `WriteLine()`, `Log()`, `Activate()`, `Clear()`.

#### `src/Services/StatusBarService.cs` *(new file)*
- `InitializeAsync()`, `SetText()`, `StartAnimation()`, `StopAnimation()`,
  `ReportProgress()`, `ClearProgress()`, `RunWithAnimationAsync()`.

### Changed

- `MyPackage.cs` – thêm `OutputWindow`, `StatusBar` properties + wire-up.
- `SampleToolWindowState.cs` – thêm `OutputWindow`, `StatusBar`.
- `SampleToolWindowControl.xaml` – thêm 5 button Output/StatusBar.
- `SampleToolWindowControl.xaml.cs` – thêm handlers tương ứng.

---

## [1.1] – baseline

- Async Tool Window cơ bản với button "Show VS Location".
- AsyncPackage load trên background thread.
- Command `ShowToolWindow` trong menu *View > Other Windows*.
