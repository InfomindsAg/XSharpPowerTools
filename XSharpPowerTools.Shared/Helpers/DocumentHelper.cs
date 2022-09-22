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

        public static async Task InsertUsingAsync(string namespaceRef, string type, XSModel xsModel)
        {
            var documentView = await VS.Documents.GetActiveDocumentViewAsync();
            var fileName = documentView?.FilePath;
            var textView = documentView?.TextView;
            var usings = new List<ITextSnapshotLine>();

            if (textView == null)
                return;

            if (await xsModel.FileContainsUsingAsync(fileName, namespaceRef))
            {
                await VS.MessageBox.ShowAsync("Using with given namespace already found in current document.");
                return;
            }

            foreach (var line in textView.TextSnapshot.Lines)
            {
                var lineText = line.GetText().Trim();
                if (lineText.StartsWith("using"))
                {
                    if (lineText.Split(' ').ElementAtOrDefault(1) == namespaceRef)
                    {
                        await VS.MessageBox.ShowAsync("Using with given namespace already found in current document.");
                        return;
                    }
                    usings.Add(line);
                }
                else
                {
                    break;
                }
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
                var insertPos = usings.LastOrDefault()?.EndIncludingLineBreak.Position ?? 0;
                var paddingNum = usings.LastOrDefault()?.GetText().TakeWhile(char.IsWhiteSpace).Count();
                var padding = paddingNum.HasValue ? new string(' ', paddingNum.Value) : string.Empty;

                if (!textView.Selection.IsEmpty)
                    edit.Replace(textView.Selection.SelectedSpans.FirstOrDefault(), type);

                edit.Insert(insertPos, $"{padding}using {namespaceRef}{Environment.NewLine}");
                edit.Apply();
            }
            catch (Exception)
            {
                edit.Cancel();
            }
        }

        public static async Task InsertNamespaceReferenceAsync(string namespaceRef, string type)
        {
            var documentView = await VS.Documents.GetActiveDocumentViewAsync();
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

        public static async Task InsertCodeSuggestionAsync(string codeSuggestion)
        {
            var documentView = await VS.Documents.GetActiveDocumentViewAsync();
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
            var relativeCaretWordPosition = GetRelativeCaretWordPosition(caretLine, caretPosition);

            try
            {
                var spanToReplace = textView.Selection.IsEmpty 
                    ? new Span(caretLine.Start + relativeCaretWordPosition, GetCaretWord(lines, caretPosition).Length)
                    : textView.Selection.SelectedSpans.FirstOrDefault();

                edit.Replace(spanToReplace, codeSuggestion);
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

            if (string.IsNullOrEmpty(lineText))
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
