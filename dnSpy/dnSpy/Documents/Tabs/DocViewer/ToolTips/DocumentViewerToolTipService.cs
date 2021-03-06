﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using dnSpy.Contracts.Command;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.Tabs.DocViewer.ToolTips;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Language.Intellisense;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Text.Classification;
using dnSpy.Contracts.Text.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace dnSpy.Documents.Tabs.DocViewer.ToolTips {
	[ExportCommandTargetFilterProvider(CommandConstants.CMDTARGETFILTER_ORDER_DOCUMENTVIEWER - 1)]
	sealed class DocumentViewerToolTipServiceCommandTargetFilterProvider : ICommandTargetFilterProvider {
		readonly Lazy<DocumentViewerToolTipServiceProvider> documentViewerToolTipServiceProvider;

		[ImportingConstructor]
		DocumentViewerToolTipServiceCommandTargetFilterProvider(Lazy<DocumentViewerToolTipServiceProvider> documentViewerToolTipServiceProvider) {
			this.documentViewerToolTipServiceProvider = documentViewerToolTipServiceProvider;
		}

		public ICommandTargetFilter Create(object target) {
			var textView = target as ITextView;
			if (textView?.Roles.Contains(PredefinedDsTextViewRoles.DocumentViewer) != true)
				return null;

			return new DocumentViewerToolTipServiceCommandTargetFilter(documentViewerToolTipServiceProvider.Value, textView);
		}
	}

	sealed class DocumentViewerToolTipServiceCommandTargetFilter : ICommandTargetFilter {
		readonly ITextView textView;
		readonly DocumentViewerToolTipServiceProvider documentViewerToolTipServiceProvider;

		public DocumentViewerToolTipServiceCommandTargetFilter(DocumentViewerToolTipServiceProvider documentViewerToolTipServiceProvider, ITextView textView) {
			if (documentViewerToolTipServiceProvider == null)
				throw new ArgumentNullException(nameof(documentViewerToolTipServiceProvider));
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			this.documentViewerToolTipServiceProvider = documentViewerToolTipServiceProvider;
			this.textView = textView;
		}

		DocumentViewerToolTipService TryGetInstance() {
			if (__documentViewerToolTipService == null) {
				var docViewer = textView.TextBuffer.TryGetDocumentViewer();
				if (docViewer != null)
					__documentViewerToolTipService = documentViewerToolTipServiceProvider.GetService(docViewer);
			}
			return __documentViewerToolTipService;
		}
		DocumentViewerToolTipService __documentViewerToolTipService;

		public CommandTargetStatus CanExecute(Guid group, int cmdId) {
			var service = TryGetInstance();
			if (service == null)
				return CommandTargetStatus.NotHandled;

			if (group == CommandConstants.TextEditorGroup) {
				switch ((TextEditorIds)cmdId) {
				case TextEditorIds.QUICKINFO:
					return CommandTargetStatus.Handled;
				}
			}
			return CommandTargetStatus.NotHandled;
		}

		public CommandTargetStatus Execute(Guid group, int cmdId, object args = null) {
			object result = null;
			return Execute(group, cmdId, args, ref result);
		}

		public CommandTargetStatus Execute(Guid group, int cmdId, object args, ref object result) {
			var service = TryGetInstance();
			if (service == null)
				return CommandTargetStatus.NotHandled;

			if (group == CommandConstants.TextEditorGroup) {
				switch ((TextEditorIds)cmdId) {
				case TextEditorIds.QUICKINFO:
					if (service.TryTriggerQuickInfo())
						return CommandTargetStatus.Handled;
					break;
				}
			}
			return CommandTargetStatus.NotHandled;
		}

		public void SetNextCommandTarget(ICommandTarget commandTarget) { }
		public void Dispose() { }
	}

	[Export(typeof(DocumentViewerToolTipServiceProvider))]
	sealed class DocumentViewerToolTipServiceProvider {
		readonly IDotNetImageService dotNetImageService;
		readonly ICodeToolTipSettings codeToolTipSettings;
		readonly IQuickInfoBroker quickInfoBroker;
		readonly IClassificationFormatMapService classificationFormatMapService;
		readonly IThemeClassificationTypeService themeClassificationTypeService;
		readonly Lazy<IDocumentViewerToolTipProvider, IDocumentViewerToolTipProviderMetadata>[] documentViewerToolTipProviders;

		[ImportingConstructor]
		DocumentViewerToolTipServiceProvider(IDotNetImageService dotNetImageService, ICodeToolTipSettings codeToolTipSettings, IQuickInfoBroker quickInfoBroker, IClassificationFormatMapService classificationFormatMapService, IThemeClassificationTypeService themeClassificationTypeService, [ImportMany] IEnumerable<Lazy<IDocumentViewerToolTipProvider, IDocumentViewerToolTipProviderMetadata>> documentViewerToolTipProviders) {
			this.dotNetImageService = dotNetImageService;
			this.codeToolTipSettings = codeToolTipSettings;
			this.quickInfoBroker = quickInfoBroker;
			this.classificationFormatMapService = classificationFormatMapService;
			this.themeClassificationTypeService = themeClassificationTypeService;
			this.documentViewerToolTipProviders = documentViewerToolTipProviders.OrderBy(a => a.Metadata.Order).ToArray();
		}

		public DocumentViewerToolTipService GetService(IDocumentViewer documentViewer) =>
			documentViewer.TextView.Properties.GetOrCreateSingletonProperty(typeof(DocumentViewerToolTipService), () => new DocumentViewerToolTipService(dotNetImageService, codeToolTipSettings, documentViewerToolTipProviders, documentViewer, quickInfoBroker, classificationFormatMapService.GetClassificationFormatMap(AppearanceCategoryConstants.QuickInfoToolTip), themeClassificationTypeService));
	}

	[Export(typeof(IQuickInfoSourceProvider))]
	[Name(PredefinedDsQuickInfoSourceProviders.DocumentViewer)]
	[ContentType(ContentTypes.Any)]
	sealed class DocumentViewerToolTipServiceQuickInfoSourceProvider : IQuickInfoSourceProvider {
		readonly DocumentViewerToolTipServiceProvider documentViewerToolTipServiceProvider;

		[ImportingConstructor]
		DocumentViewerToolTipServiceQuickInfoSourceProvider(DocumentViewerToolTipServiceProvider documentViewerToolTipServiceProvider) {
			this.documentViewerToolTipServiceProvider = documentViewerToolTipServiceProvider;
		}

		public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			var docViewer = textBuffer.TryGetDocumentViewer();
			if (docViewer == null)
				return null;
			return new DocumentViewerToolTipServiceQuickInfoSource(documentViewerToolTipServiceProvider.GetService(docViewer));
		}
	}

	sealed class DocumentViewerToolTipServiceQuickInfoSource : IQuickInfoSource {
		readonly DocumentViewerToolTipService documentViewerToolTipService;

		public DocumentViewerToolTipServiceQuickInfoSource(DocumentViewerToolTipService documentViewerToolTipService) {
			if (documentViewerToolTipService == null)
				throw new ArgumentNullException(nameof(documentViewerToolTipService));
			this.documentViewerToolTipService = documentViewerToolTipService;
		}

		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) =>
			documentViewerToolTipService.AugmentQuickInfoSession(session, quickInfoContent, out applicableToSpan);

		public void Dispose() { }
	}

	sealed class DocumentViewerToolTipService {
		readonly IDotNetImageService dotNetImageService;
		readonly ICodeToolTipSettings codeToolTipSettings;
		readonly Lazy<IDocumentViewerToolTipProvider, IDocumentViewerToolTipProviderMetadata>[] documentViewerToolTipProviders;
		readonly IDocumentViewer documentViewer;
		readonly IQuickInfoBroker quickInfoBroker;
		readonly IClassificationFormatMap classificationFormatMap;
		readonly IThemeClassificationTypeService themeClassificationTypeService;

		public DocumentViewerToolTipService(IDotNetImageService dotNetImageService, ICodeToolTipSettings codeToolTipSettings, Lazy<IDocumentViewerToolTipProvider, IDocumentViewerToolTipProviderMetadata>[] documentViewerToolTipProviders, IDocumentViewer documentViewer, IQuickInfoBroker quickInfoBroker, IClassificationFormatMap classificationFormatMap, IThemeClassificationTypeService themeClassificationTypeService) {
			if (dotNetImageService == null)
				throw new ArgumentNullException(nameof(dotNetImageService));
			if (codeToolTipSettings == null)
				throw new ArgumentNullException(nameof(codeToolTipSettings));
			if (documentViewerToolTipProviders == null)
				throw new ArgumentNullException(nameof(documentViewerToolTipProviders));
			if (documentViewer == null)
				throw new ArgumentNullException(nameof(documentViewer));
			if (quickInfoBroker == null)
				throw new ArgumentNullException(nameof(quickInfoBroker));
			if (classificationFormatMap == null)
				throw new ArgumentNullException(nameof(classificationFormatMap));
			if (themeClassificationTypeService == null)
				throw new ArgumentNullException(nameof(themeClassificationTypeService));
			this.dotNetImageService = dotNetImageService;
			this.codeToolTipSettings = codeToolTipSettings;
			this.documentViewerToolTipProviders = documentViewerToolTipProviders;
			this.documentViewer = documentViewer;
			this.quickInfoBroker = quickInfoBroker;
			this.classificationFormatMap = classificationFormatMap;
			this.themeClassificationTypeService = themeClassificationTypeService;
		}

		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
			applicableToSpan = null;
			Debug.Assert(session.TextView == documentViewer.TextView);
			if (session.TextView != documentViewer.TextView)
				return;
			var snapshot = session.TextView.TextSnapshot;
			var point = session.GetTriggerPoint(snapshot);
			if (point == null)
				return;
			var spanData = GetReference(point.Value.Position, false);
			if (spanData == null)
				return;
			var info = spanData.Value;
			Debug.Assert(info.Span.End <= snapshot.Length);
			if (info.Span.End > snapshot.Length)
				return;

			var toolTipContent = CreateToolTipContent(GetDecompiler(), info.Data.Reference);
			if (toolTipContent == null)
				return;

			quickInfoContent.Add(toolTipContent);
			applicableToSpan = snapshot.CreateTrackingSpan(info.Span, SpanTrackingMode.EdgeInclusive);
		}

		public bool TryTriggerQuickInfo() {
			if (documentViewer.TextView.IsClosed)
				return false;
			var caretPos = documentViewer.TextView.Caret.Position;
			if (caretPos.VirtualSpaces > 0)
				return false;
			var pos = caretPos.BufferPosition;
			var spanData = GetReference(pos.Position, true);
			if (spanData == null)
				return false;
			var info = spanData.Value;
			var snapshot = pos.Snapshot;
			if (info.Span.End > snapshot.Length)
				return false;
			var triggerPoint = snapshot.CreateTrackingPoint(info.Span.Start, PointTrackingMode.Negative);
			var session = quickInfoBroker.TriggerQuickInfo(documentViewer.TextView, triggerPoint, trackMouse: false);
			return session?.IsDismissed == false;
		}

		SpanData<ReferenceInfo>? GetReference(int position, bool allowIntersection) => documentViewer.Content.ReferenceCollection.Find(position, allowIntersection);

		IDecompiler GetDecompiler() {
			var content = documentViewer.DocumentTab.Content as IDecompilerTabContent;
			return content == null ? null : content.Decompiler;
		}

		object CreateToolTipContent(IDecompiler decompiler, object @ref) {
			if (decompiler == null)
				return null;
			if (@ref == null)
				return null;

			var ctx = new ToolTipProviderContext(dotNetImageService, decompiler, codeToolTipSettings, documentViewer, classificationFormatMap, themeClassificationTypeService);
			foreach (var provider in documentViewerToolTipProviders) {
				var toolTipContent = provider.Value.Create(ctx, @ref);
				if (toolTipContent != null)
					return toolTipContent;
			}

			return null;
		}
	}
}
