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
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using dnSpy.Contracts.Language.Intellisense;
using dnSpy.Contracts.Text.Classification;
using dnSpy.Text.MEF;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace dnSpy.Language.Intellisense {
	[Export(typeof(ISignatureHelpBroker))]
	sealed class SignatureHelpBroker : ISignatureHelpBroker, ISignatureHelpPresenterProvider {
		readonly Lazy<IIntellisenseSessionStackMapService> intellisenseSessionStackMapService;
		readonly Lazy<ISignatureHelpSourceProvider, IOrderableContentTypeMetadata>[] signatureHelpSourceProviders;
		readonly ITextBufferFactoryService textBufferFactoryService;
		readonly IContentTypeRegistryService contentTypeRegistryService;
		readonly IClassifierAggregatorService classifierAggregatorService;
		readonly IClassificationFormatMapService classificationFormatMapService;

		[ImportingConstructor]
		SignatureHelpBroker(Lazy<IIntellisenseSessionStackMapService> intellisenseSessionStackMapService, [ImportMany] IEnumerable<Lazy<ISignatureHelpSourceProvider, IOrderableContentTypeMetadata>> signatureHelpSourceProviders, ITextBufferFactoryService textBufferFactoryService, IContentTypeRegistryService contentTypeRegistryService, IClassifierAggregatorService classifierAggregatorService, IClassificationFormatMapService classificationFormatMapService) {
			this.intellisenseSessionStackMapService = intellisenseSessionStackMapService;
			this.signatureHelpSourceProviders = Orderer.Order(signatureHelpSourceProviders).ToArray();
			this.textBufferFactoryService = textBufferFactoryService;
			this.contentTypeRegistryService = contentTypeRegistryService;
			this.classifierAggregatorService = classifierAggregatorService;
			this.classificationFormatMapService = classificationFormatMapService;
		}

		public ISignatureHelpSession TriggerSignatureHelp(ITextView textView) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			var triggerPoint = textView.TextSnapshot.CreateTrackingPoint(textView.Caret.Position.BufferPosition.Position, PointTrackingMode.Negative, TrackingFidelityMode.Forward);
			return TriggerSignatureHelp(textView, triggerPoint, trackCaret: true);
		}

		public ISignatureHelpSession TriggerSignatureHelp(ITextView textView, ITrackingPoint triggerPoint, bool trackCaret) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			if (triggerPoint == null)
				throw new ArgumentNullException(nameof(triggerPoint));
			var session = CreateSignatureHelpSession(textView, triggerPoint, trackCaret);
			session.Start();
			return session.IsDismissed ? null : session;
		}

		public ISignatureHelpSession CreateSignatureHelpSession(ITextView textView, ITrackingPoint triggerPoint, bool trackCaret) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			if (triggerPoint == null)
				throw new ArgumentNullException(nameof(triggerPoint));
			var stack = intellisenseSessionStackMapService.Value.GetStackForTextView(textView);
			var session = new SignatureHelpSession(textView, triggerPoint, trackCaret, this, signatureHelpSourceProviders);
			stack.PushSession(session);
			return session;
		}

		public void DismissAllSessions(ITextView textView) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			foreach (var session in GetSessions(textView))
				session.Dismiss();
		}

		public bool IsSignatureHelpActive(ITextView textView) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			return GetSessions(textView).Count != 0;
		}

		public ReadOnlyCollection<ISignatureHelpSession> GetSessions(ITextView textView) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			var stack = intellisenseSessionStackMapService.Value.GetStackForTextView(textView);
			return new ReadOnlyCollection<ISignatureHelpSession>(stack.Sessions.OfType<ISignatureHelpSession>().ToArray());
		}

		IIntellisensePresenter ISignatureHelpPresenterProvider.Create(ISignatureHelpSession signatureHelpSession) {
			if (signatureHelpSession == null)
				throw new ArgumentNullException(nameof(signatureHelpSession));
			return new SignatureHelpPresenter(signatureHelpSession, textBufferFactoryService, contentTypeRegistryService, classifierAggregatorService, classificationFormatMapService.GetClassificationFormatMap(AppearanceCategoryConstants.SignatureHelpToolTip));
		}
	}
}
