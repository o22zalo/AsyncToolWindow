# Hướng dẫn: Settings Dialog (§Settings)

**Tính năng:** WPF Modal Dialog mở từ Tools menu — giống WakaTime Settings  
**Ngày:** 2026-03-19  
**Phiên bản:** `feat: settings-dialog`

---

## Mục tiêu

Thêm **Settings dialog** dạng popup modal xuất hiện trong **Tools menu** của Visual Studio,  
cho phép người dùng thay đổi cấu hình extension trực tiếp — tương tự `WakaTime > Settings`.

---

## Cách mở

**Tools menu → Async Tool Window Sample Settings...**

```
Tools
├── Get Tools and Features...
├── Extensions and Updates...
├── WakaTime               ▶  Settings   ← kiểu này
├── ...
└── Async Tool Window Sample Settings...  ← dialog của chúng ta
```

---

## File mới / thay đổi

| File | Loại thay đổi |
|------|---------------|
| `src/ToolWindows/SettingsDialog.xaml` | **Mới** – WPF Window UI |
| `src/ToolWindows/SettingsDialog.xaml.cs` | **Mới** – code-behind |
| `src/Commands/ShowSettings.cs` | **Mới** – command đăng ký menu item |
| `src/PackageGuids.cs` | Thêm `CmdIdSettings = 0x0400` |
| `src/VSCommandTable.vsct` | Thêm Group `SettingsCmdGroup` + Button `CmdIdSettings` |
| `src/MyPackage.cs` | Thêm `await ShowSettings.InitializeAsync(this)` |
| `src/AsyncToolWindowSample.csproj` | Thêm Compile + Page entries, thêm `System.Xml.Linq` |

---

## Layout dialog

```
┌─────────────────────────────────────────────────┐
│  ⚙  Settings                                    │
├─────────────────────────────────────────────────┤
│  Server URL :   [https://api.example.com/v1   ] │
│                 Example: https://api.example... │
│  API Key :      [●●●●●●●●●●●●●●●●●●●●●●●●●●] │
│                 Leave blank if not required.    │
│  Timeout (s) :  [30  ]                          │
│                 Valid: 1–300. Default: 30.      │
│  Max Results :  [50  ]                          │
│                 Valid: 1–1000. Default: 50.     │
│  Output Format: [JSON ▼]                        │
│                 Format used for Output Window.  │
│                 ☑ Debug                         │
│                 ☑ Show Status Bar               │
├─────────────────────────────────────────────────┤
│                        [  Save  ]  [  Cancel  ] │
└─────────────────────────────────────────────────┘
```

---

## Kiến trúc

```
VSCommandTable.vsct
  └─ SettingsCmdGroup (parent = Tools menu, priority 0x0800)
       └─ CmdIdSettings (0x0400) "Async Tool Window Sample Settings..."

ShowSettings.cs (MenuCommand)
  └─ Execute() → new SettingsDialog(...).ShowDialog()

SettingsDialog.xaml  (WPF Window, modal)
  └─ OnLoaded()   → load từ ConfigurationService
  └─ BtnSave()    → validate → Config.Set(...) × N → Close
  └─ BtnCancel()  → Close
```

---

## Validation

| Field | Rule |
|-------|------|
| Server URL | Không trống, bắt đầu `http://` hoặc `https://` |
| Timeout | Số nguyên 1–300 |
| Max Results | Số nguyên 1–1000 |

Lỗi validation hiện inline trong dialog (border màu cam), không throw exception.

---

## Thread Safety

- `Execute()` trong `ShowSettings` — gọi trên UI thread (MenuCommand handler luôn là UI thread)
- `ShowDialog()` — blocking modal, trả về sau khi user Save/Cancel
- `Config.Set()` — yêu cầu UI thread, đảm bảo vì gọi trong `BtnSave_Click`

---

## Khác biệt với ConfigEditorWindow

| | SettingsDialog | ConfigEditorWindow |
|---|---|---|
| Loại | WPF Window (modal popup) | ToolWindowPane (dock panel) |
| Mở từ | Tools menu | View > Other Windows |
| UX | Blocking, đóng sau Save/Cancel | Non-blocking, luôn mở |
| Phù hợp | Cài đặt nhanh | Xem/test config chi tiết |
