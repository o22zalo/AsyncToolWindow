# Changelog

All notable changes to **AsyncToolWindowSample** are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased] – 2026-03-19 (patch)

### Fixed

#### `src/Services/StatusBarService.cs`
- **CS0165 – Use of unassigned local variable 'frozen':**
  Đổi `_statusBar?.IsFrozen(out int frozen)` thành `int frozen = 0; _statusBar?.IsFrozen(out frozen)`.
  Nguyên nhân: null-conditional operator `?.` có thể bỏ qua lời gọi, khiến `out` param không bao giờ được gán; compiler bắt đúng lỗi.
- **CS1503 (×2) – Argument 1: cannot convert from 'ref ulong' to 'ref uint':**
  `IVsStatusbar.Progress` khai báo tham số đầu là `ref uint`, không phải `ref ulong`.
  Đổi signature `ReportProgress(ref ulong cookie, …)` và `ClearProgress(ref ulong cookie)` thành `ref uint cookie`.

#### `src/ToolWindows/SampleToolWindowControl.xaml.cs`
- Đổi khai báo `ulong cookie = 0` → `uint cookie = 0` trong `Button_Progress_Click`
  để khớp với signature đã sửa của `StatusBarService`.



All notable changes to **AsyncToolWindowSample** are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased] – 2026-03-19

### Added

#### `src/Services/OutputWindowService.cs` *(new file)*
- **Mục đích:** Tách biệt logic tương tác với Output Window thành một service riêng, tuân theo nguyên tắc Single Responsibility.
- **Nội dung:**
  - `OutputWindowService(AsyncPackage)` – constructor nhận package để gọi `GetServiceAsync`.
  - `InitializeAsync()` – tạo (hoặc lấy lại) custom Output pane bằng `SVsOutputWindow` / `IVsOutputWindow.CreatePane`. Switch sang UI thread nội bộ; an toàn khi gọi từ background thread.
  - `WriteLine(string)` – ghi một dòng text (tự thêm `\n`).
  - `Log(string)` – ghi dòng text có timestamp `[HH:mm:ss]` – tiện cho log dạng journal.
  - `Activate()` – đưa pane về foreground trong Output Window.
  - `Clear()` – xóa toàn bộ nội dung pane.
- **Lý do:** Trước đây không có Output Window. Yêu cầu bổ sung demo custom pane theo pattern AsyncPackage.

#### `src/Services/StatusBarService.cs` *(new file)*
- **Mục đích:** Wrapper mỏng quanh `IVsStatusbar` giúp callers không cần thao tác COM trực tiếp.
- **Nội dung:**
  - `InitializeAsync()` – resolve `SVsStatusbar` service.
  - `SetText(string)` – unfreeze rồi ghi text vào status bar.
  - `StartAnimation(string)` / `StopAnimation()` – bật/tắt icon xoay `SBAI_General`.
  - `ReportProgress(ref ulong, string, uint, uint)` – hiện progress bar.
  - `ClearProgress(ref ulong)` – ẩn progress bar.
  - `RunWithAnimationAsync(Func<Task>, string)` – convenience method: chạy async work trên background thread, tự động bật/tắt animation và reset text về "Ready".
- **Lý do:** Cần demo Status Bar theo tài liệu Microsoft VS SDK.

### Changed

#### `src/MyPackage.cs`
- **Thêm** hai property `OutputWindow` và `StatusBar` (singleton, set trong `InitializeAsync`).
- **`InitializeAsync`:** khởi tạo cả hai service trước khi switch sang UI thread; sau khi load ghi log vào Output pane và set status bar text.
- **`InitializeToolWindowAsync`:** truyền `OutputWindow` và `StatusBar` vào `SampleToolWindowState` để tool window control có thể dùng.
- **Lý do:** Package là single source of truth cho các VS services; tránh service locator rải rác trong codebase.

#### `src/ToolWindows/SampleToolWindowState.cs`
- **Thêm** hai property `OutputWindowService OutputWindow` và `StatusBarService StatusBar`.
- **Lý do:** State object là cầu nối giữa `MyPackage.InitializeToolWindowAsync` (background thread) và constructor của `SampleToolWindow`/Control (UI thread). Thêm services vào đây đảm bảo không cần static/singleton toàn cục.

#### `src/ToolWindows/SampleToolWindowControl.xaml`
- **Thêm** 5 button mới nhóm theo "Output Window" và "Status Bar":
  - *Write to Output* – ghi log vào custom pane.
  - *Clear Output Pane* – xóa pane.
  - *Set Status Text* – cập nhật text status bar.
  - *Animate (3 s)* – chạy animation 3 giây (demo background work).
  - *Show Progress Bar* – demo progress bar 5 bước.
- **Lý do:** Cần UI để người dùng tương tác và kiểm tra hai feature mới.

#### `src/ToolWindows/SampleToolWindowControl.xaml.cs`
- **Đổi tên** `Button_Click` → `Button_ShowVsLocation_Click` (rõ ràng hơn); xóa handler trùng lặp `Button_Click_1`.
- **Thêm** handlers:
  - `Button_WriteOutput_Click` – gọi `OutputWindow.Activate()`, `.Log()`, `.WriteLine()` và cập nhật status bar.
  - `Button_ClearOutput_Click` – gọi `OutputWindow.Clear()` + `.Log()`.
  - `Button_SetStatus_Click` – gọi `StatusBar.SetText()`.
  - `Button_Animate_Click` – fire-and-forget qua `JoinableTaskFactory.RunAsync`, gọi `StatusBar.RunWithAnimationAsync` với delay 3 s.
  - `Button_Progress_Click` – fire-and-forget, vòng lặp 5 bước gọi `StatusBar.ReportProgress` + delay 600 ms mỗi bước.
- **Lý do:** Minh họa đầy đủ API Output Window và Status Bar trên UI thread đúng cách (không block, dùng JoinableTaskFactory).

---

## [1.1] – baseline (trước thay đổi này)

- Async Tool Window cơ bản với một button "Click me" hiển thị đường dẫn VS.
- AsyncPackage load trên background thread.
- Command `ShowToolWindow` trong menu *View > Other Windows*.
