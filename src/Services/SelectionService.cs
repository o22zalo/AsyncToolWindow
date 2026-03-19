using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.Services
{
    /// <summary>
    /// Provides unified access to the VS editor selection via two tiers:
    /// <list type="bullet">
    ///   <item>Tier 1 – DTE <see cref="TextSelection"/> (COM, simple)</item>
    ///   <item>Tier 2 – <see cref="IWpfTextView"/> MEF API (precise, managed)</item>
    /// </list>
    /// All public methods must be called on the UI thread.
    /// </summary>
    public sealed class SelectionService
    {
        private readonly AsyncPackage _package;

        // IServiceProvider (public interface on AsyncPackage) — used for GetService calls
        private readonly IServiceProvider _serviceProvider;

        // MEF bridge — resolved once in InitializeAsync
        private IVsEditorAdaptersFactoryService _adaptersFactory;

        public SelectionService(AsyncPackage package)
        {
            _package         = package ?? throw new ArgumentNullException(nameof(package));
            _serviceProvider = package;   // AsyncPackage implements IServiceProvider (public)
        }

        // ------------------------------------------------------------------ //
        //  Initialization                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Resolves MEF services needed for Tier-2 API.
        /// Switch to UI thread is handled internally.
        /// </summary>
        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var componentModel =
                await _package.GetServiceAsync(typeof(SComponentModel))
                as IComponentModel;

            _adaptersFactory =
                componentModel?.GetService<IVsEditorAdaptersFactoryService>();
        }

        // ================================================================== //
        //  TIER 1 — DTE TextSelection                                         //
        // ================================================================== //

        /// <summary>
        /// Returns the <see cref="TextSelection"/> of the active document,
        /// or <c>null</c> when no text document is active.
        /// </summary>
        public TextSelection GetDteSelection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            return dte?.ActiveDocument?.Selection as TextSelection;
        }

        /// <summary>
        /// Snapshot of caret position (1-based) from the DTE layer.
        /// Returns <c>null</c> when no text document is active.
        /// </summary>
        public DteCaretInfo GetDteCaretInfo()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sel = GetDteSelection();
            if (sel == null) return null;

            return new DteCaretInfo
            {
                Line   = sel.CurrentLine,
                Column = sel.CurrentColumn,
                AnchorLine         = sel.AnchorPoint.Line,
                AnchorDisplayColumn= sel.AnchorPoint.DisplayColumn,
                AnchorAbsOffset    = sel.AnchorPoint.AbsoluteCharOffset,
                ActiveLine         = sel.ActivePoint.Line,
                ActiveAbsOffset    = sel.ActivePoint.AbsoluteCharOffset,
                TopLine            = sel.TopLine,
                BottomLine         = sel.BottomLine,
                SelectedText       = sel.Text,
                IsEmpty            = string.IsNullOrEmpty(sel.Text),
                Mode               = sel.Mode.ToString()
            };
        }

        /// <summary>
        /// Navigates the caret to the specified 1-based line.
        /// </summary>
        public void GotoLine(int line, bool select = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetDteSelection()?.GotoLine(line, select);
        }

        /// <summary>Selects all text in the active document (DTE).</summary>
        public void SelectAll()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetDteSelection()?.SelectAll();
        }

        /// <summary>Selects the current line (DTE).</summary>
        public void SelectCurrentLine()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetDteSelection()?.SelectLine();
        }

        /// <summary>Clears the selection, leaving caret at ActivePoint (DTE).</summary>
        public void CollapseSelection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetDteSelection()?.Collapse();
        }

        /// <summary>
        /// Finds the first occurrence of <paramref name="pattern"/> in the active document.
        /// Returns <c>true</c> when found.
        /// </summary>
        public bool FindText(string pattern, bool matchCase = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var sel = GetDteSelection();
            if (sel == null) return false;

            int flags = matchCase
                ? (int)vsFindOptions.vsFindOptionsMatchCase
                : (int)vsFindOptions.vsFindOptionsNone;

            return sel.FindText(pattern, flags);
        }

        // ================================================================== //
        //  TIER 2 — IWpfTextView MEF API                                      //
        // ================================================================== //

        /// <summary>
        /// Returns the active <see cref="IWpfTextView"/>, or <c>null</c> when
        /// there is no focused editor pane.
        /// </summary>
        public IWpfTextView GetActiveWpfTextView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_adaptersFactory == null)
                return null;

            var textManager =
                _serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null) return null;

            textManager.GetActiveView(fMustHaveFocus: 1, pBuffer: null,
                ppView: out IVsTextView vsView);

            return vsView == null ? null : _adaptersFactory.GetWpfTextView(vsView);
        }

        /// <summary>
        /// Snapshot of caret position (0-based) from the IWpfTextView layer.
        /// Returns <c>null</c> when no editor pane has focus.
        /// </summary>
        public MefCaretInfo GetMefCaretInfo()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = GetActiveWpfTextView();
            if (view == null) return null;

            var snapshot   = view.TextBuffer.CurrentSnapshot;
            var caretPos   = view.Caret.Position.BufferPosition;
            var caretLine  = caretPos.GetContainingLine();

            return new MefCaretInfo
            {
                Offset0Based     = caretPos.Position,
                LineNumber0Based  = caretLine.LineNumber,
                Column0Based      = caretPos.Position - caretLine.Start.Position,
                TotalChars        = snapshot.Length,
                TotalLines        = snapshot.LineCount,
                ContentType       = view.TextBuffer.ContentType.TypeName
            };
        }

        /// <summary>
        /// Returns all selected spans from the active <see cref="IWpfTextView"/>.
        /// An empty list means no selection (not no view).
        /// </summary>
        public IReadOnlyList<SelectionSpanInfo> GetMefSelectedSpans()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = new List<SelectionSpanInfo>();

            var view = GetActiveWpfTextView();
            if (view == null) return result;

            foreach (var span in view.Selection.SelectedSpans)
            {
                result.Add(new SelectionSpanInfo
                {
                    Start     = span.Start.Position,
                    End       = span.End.Position,
                    Length    = span.Length,
                    Text      = span.GetText(),
                    StartLine = span.Start.GetContainingLine().LineNumber,
                    EndLine   = span.End.GetContainingLine().LineNumber
                });
            }

            return result;
        }

        /// <summary>
        /// Inserts <paramref name="text"/> at the current caret position using
        /// an <see cref="ITextEdit"/> transaction (Tier 2).
        /// </summary>
        public void InsertAtCaret(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = GetActiveWpfTextView();
            if (view == null) return;

            var caretPos = view.Caret.Position.BufferPosition.Position;
            using (var edit = view.TextBuffer.CreateEdit())
            {
                edit.Insert(caretPos, text);
                edit.Apply();
            }
        }

        /// <summary>
        /// Replaces the first selected span (if any) with <paramref name="replacement"/>
        /// using an <see cref="ITextEdit"/> transaction (Tier 2).
        /// </summary>
        public void ReplaceSelection(string replacement)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = GetActiveWpfTextView();
            if (view == null || view.Selection.IsEmpty) return;

            var span = view.Selection.StreamSelectionSpan.SnapshotSpan;
            using (var edit = view.TextBuffer.CreateEdit())
            {
                edit.Replace(span, replacement);
                edit.Apply();
            }
        }

        /// <summary>
        /// Returns the full text of the active buffer (Tier 2).
        /// Returns <c>null</c> when no editor has focus.
        /// </summary>
        public string GetBufferText()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetActiveWpfTextView()?.TextBuffer.CurrentSnapshot.GetText();
        }
    }

    // ====================================================================== //
    //  Data transfer objects                                                   //
    // ====================================================================== //

    /// <summary>Caret/selection information from the DTE layer (1-based).</summary>
    public sealed class DteCaretInfo
    {
        public int    Line               { get; set; }
        public int    Column             { get; set; }
        public int    AnchorLine         { get; set; }
        public int    AnchorDisplayColumn{ get; set; }
        public int    AnchorAbsOffset    { get; set; }
        public int    ActiveLine         { get; set; }
        public int    ActiveAbsOffset    { get; set; }
        public int    TopLine            { get; set; }
        public int    BottomLine         { get; set; }
        public string SelectedText       { get; set; }
        public bool   IsEmpty            { get; set; }
        public string Mode               { get; set; }
    }

    /// <summary>Caret information from the IWpfTextView layer (0-based).</summary>
    public sealed class MefCaretInfo
    {
        public int    Offset0Based      { get; set; }
        public int    LineNumber0Based  { get; set; }
        public int    Column0Based      { get; set; }
        public int    TotalChars        { get; set; }
        public int    TotalLines        { get; set; }
        public string ContentType       { get; set; }
    }

    /// <summary>A single selected span from <see cref="ITextSelection.SelectedSpans"/>.</summary>
    public sealed class SelectionSpanInfo
    {
        public int    Start     { get; set; }
        public int    End       { get; set; }
        public int    Length    { get; set; }
        public string Text      { get; set; }
        public int    StartLine { get; set; }
        public int    EndLine   { get; set; }
    }
}
