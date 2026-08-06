using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.Helpers
{
    public static class DocumentHelper
    {
        private static readonly Regex UsingLineRegex = new(@"^\s*using\s+(?:static\s+)?(?<ns>[^\s/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static async Task OpenProjectItemAtAsync(string file, int lineIndex, string sourceCode, string keyWord)
        {
            if (string.IsNullOrEmpty(file))
                return;

            file = file.Trim();
            if (!System.IO.File.Exists(file))
                return;

            WindowFrame editorWindow = null;
            if (!await VS.Documents.IsOpenAsync(file))
                editorWindow = (await VS.Documents.OpenViaProjectAsync(file) ?? await VS.Documents.OpenAsync(file))?.WindowFrame;

            if (editorWindow == null)
                editorWindow = await VS.Windows.FindDocumentWindowAsync(file);

            if (editorWindow == null)
            {
                await VS.MessageBox.ShowWarningAsync("X# Code Browser", "Failed to open file.");
                return;
            }

            await editorWindow.ShowAsync();

            var textView = (await VS.Documents.GetDocumentViewAsync(file))?.TextView;
            var lines = textView.TextSnapshot.Lines;
            var linesToSearch = lines.GetRange(lineIndex - 1, 50);

            var sourceCodeLines = sourceCode.Split(';');

            if (sourceCodeLines.Length > 1)
                sourceCode = sourceCodeLines.Where(q => q.Contains(keyWord)).FirstOrDefault()?.Trim();

            var caretLine = linesToSearch.Where(q => q.GetText().Trim() == sourceCode).FirstOrDefault() ?? lines.ElementAt(lineIndex);

            textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(caretLine.Start, caretLine.End));
            textView.Caret.MoveTo(caretLine.End);
            textView.VisualElement.Focus();
        }

        public static async Task InsertUsingAsync(string namespaceRef, string type)
        {
            var textView = (await VS.Documents.GetActiveDocumentViewAsync())?.TextView;

            if (textView == null)
                return;

            var insertion = GetUsingInsertion(textView.TextSnapshot, namespaceRef);

            if (insertion == null)
            {
                await VS.MessageBox.ShowAsync("Using with given namespace already found in current document.");
                return;
            }

            ITextEdit edit;
            try
            {
                edit = textView.TextBuffer.CreateEdit();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            try
            {
                if (!textView.Selection.IsEmpty)
                    edit.Replace(textView.Selection.SelectedSpans.FirstOrDefault(), type);

                edit.Insert(insertion.Value.Position, insertion.Value.Text);
                edit.Apply();
            }
            catch (Exception)
            {
                edit.Cancel();
            }
        }

        /// <summary>
        /// Determines where a new using has to be inserted. The file header (blank lines, comments
        /// and preprocessor directives) is skipped, so usings are found even when they do not start
        /// on the very first line. Returns null when the namespace is already used.
        /// </summary>
        private static (int Position, string Text)? GetUsingInsertion(ITextSnapshot snapshot, string namespaceRef)
        {
            ITextSnapshotLine lastUsing = null;
            ITextSnapshotLine firstCode = null;
            var inBlockComment = false;

            foreach (var line in snapshot.Lines)
            {
                var text = line.GetText().Trim();

                if (inBlockComment)
                {
                    var commentEnd = text.IndexOf("*/");
                    if (commentEnd < 0)
                        continue;
                    text = text.Substring(commentEnd + 2).Trim();
                    inBlockComment = false;
                }

                while (text.StartsWith("/*"))
                {
                    var commentEnd = text.IndexOf("*/", 2);
                    if (commentEnd < 0)
                    {
                        inBlockComment = true;
                        text = string.Empty;
                        break;
                    }
                    text = text.Substring(commentEnd + 2).Trim();
                }

                if (text.Length == 0 || text.StartsWith("//") || text.StartsWith("&&") || text.StartsWith("*") || text.StartsWith("#"))
                    continue;

                var match = UsingLineRegex.Match(text);
                if (!match.Success)
                {
                    firstCode = line;
                    break;
                }

                if (string.Equals(match.Groups["ns"].Value.TrimEnd(';'), namespaceRef, StringComparison.OrdinalIgnoreCase))
                    return null;

                lastUsing = line;
            }

            var firstLine = snapshot.GetLineFromLineNumber(0);
            var lineBreak = firstLine.LineBreakLength > 0 ? firstLine.GetLineBreakText() : Environment.NewLine;

            if (lastUsing != null)
            {
                var lead = lastUsing.LineBreakLength > 0 ? string.Empty : lineBreak;
                return (lastUsing.EndIncludingLineBreak.Position, $"{lead}{GetIndent(lastUsing)}using {namespaceRef}{lineBreak}");
            }

            if (firstCode != null)
                return (firstCode.Start.Position, $"{GetIndent(firstCode)}using {namespaceRef}{lineBreak}{lineBreak}");

            var lastLine = snapshot.GetLineFromLineNumber(snapshot.LineCount - 1);
            var separator = lastLine.Length > 0 && lastLine.LineBreakLength == 0 ? lineBreak : string.Empty;
            return (snapshot.Length, $"{separator}using {namespaceRef}{lineBreak}");
        }

        private static string GetIndent(ITextSnapshotLine line)
        {
            var text = line.GetText();
            return text.Substring(0, text.Length - text.TrimStart().Length);
        }

        public static async Task InsertNamespaceReferenceAsync(string namespaceRef, string type) 
        {
            var documentView = await VS.Documents.GetActiveDocumentViewAsync();
            var fileName = documentView?.FilePath;
            var textView = documentView?.TextView;

            if (textView == null)
                return;

            ITextEdit edit;
            try
            {
                edit = textView.TextBuffer?.CreateEdit();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var lines = textView.TextBuffer?.CurrentSnapshot?.Lines;
            var caretPosition = textView.Caret.Position.BufferPosition.Position;
            var caretLine = lines?.FirstOrDefault(q => q.Start.Position <= caretPosition && q.End.Position >= caretPosition);
            var caretWordPosition = GetRelativeCaretWordPosition(caretLine, caretPosition);

            try
            {
                var insertPos = caretLine.Start.Position + caretWordPosition;
                edit.Insert(insertPos, $"{namespaceRef}.");
                if (!textView.Selection.IsEmpty)
                    edit.Replace(textView.Selection.SelectedSpans.FirstOrDefault(), type);
                edit.Apply();
            }
            catch (Exception)
            {
                edit.Cancel();
            }
        }

        public static string GetCaretWord(IEnumerable<ITextSnapshotLine> lines, int? position)
        {
            if (!position.HasValue)
                return string.Empty;

            var caretPosition = position.Value;
            var caretLine = lines?.FirstOrDefault(q => q.Start.Position <= caretPosition && q.End.Position >= caretPosition);
            var lineText = caretLine?.GetText();

            if (string.IsNullOrWhiteSpace(lineText))
                return string.Empty;

            var relativeCaretPosition = caretPosition - caretLine.Start.Position;

            var words = Regex.Split(lineText, @"(\W)");

            var lengthSum = 0;
            var caretWord = string.Empty;
            foreach (var word in words) 
            { 
                if (lengthSum <= relativeCaretPosition && lengthSum + word.Length >= relativeCaretPosition && !Regex.IsMatch(word, @"\W"))
                {
                    caretWord = word;
                    break;
                }
                lengthSum += word.Length;
            }

            return caretWord;
        }

        private static int GetRelativeCaretWordPosition(ITextSnapshotLine caretLine, int caretPosition) 
        {
            var lineText = caretLine?.GetText();

            if (string.IsNullOrWhiteSpace(lineText))
                return 0;

            var relativeCaretPosition = caretPosition - caretLine.Start.Position;

            var words = Regex.Split(lineText, @"(\W)");

            var lengthSum = 0;
            foreach (var word in words)
            {
                if (lengthSum <= relativeCaretPosition && lengthSum + word.Length >= relativeCaretPosition && !Regex.IsMatch(word, @"\W"))
                    return lengthSum;
                lengthSum += word.Length;
            }
            return 0;
        }

        public static async Task<string> GetEditorSearchTermAsync()
        {
            var textView = (await VS.Documents.GetActiveDocumentViewAsync())?.TextView;

            if (textView == null)
                return string.Empty;

            return textView.Selection == null || textView.Selection.IsEmpty
                ? GetCaretWord(textView.TextBuffer?.CurrentSnapshot?.Lines, textView.Caret?.Position.BufferPosition.Position)
                : textView.Selection.VirtualSelectedSpans[0].GetText();
        }

        public static async Task<string> GetCurrentFileAsync()
        {
            var currentDocument = await VS.Documents.GetActiveDocumentViewAsync();
            return currentDocument?.FilePath;
        }

        public static async Task<int> GetCaretPositionAsync()
        {
            var textView = (await VS.Documents.GetActiveDocumentViewAsync())?.TextView;
            var position = textView.Caret?.Position.BufferPosition.Position;
            return position ?? -1;
        }
    }
}
