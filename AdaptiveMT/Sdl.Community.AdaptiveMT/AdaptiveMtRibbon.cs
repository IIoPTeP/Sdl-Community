﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Sdl.Community.AdaptiveMT.Service;
using Sdl.Community.AdaptiveMT.Service.Clients;
using Sdl.Community.AdaptiveMT.Service.Helpers;
using Sdl.Community.AdaptiveMT.Service.Model;
using Sdl.Core.Globalization;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.FileTypeSupport.Framework.Bilingual;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.IntegrationApi;
using Sdl.LanguagePlatform.Core.Tokenization;
using Sdl.LanguagePlatform.TranslationMemory;
using Sdl.LanguagePlatform.TranslationMemoryApi;
using Sdl.ProjectAutomation.AutomaticTasks;
using Sdl.ProjectAutomation.Core;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Sdl.TranslationStudioAutomation.IntegrationApi.Presentation.DefaultLocations;

namespace Sdl.Community.AdaptiveMT
{
	[Action("AdaptiveMt",
		Name = "Adaptive MT Training",
		Description = "Adaptive MT Training"
	)]
	[ActionLayout(typeof(TranslationStudioDefaultContextMenus.ProjectsContextMenuLocation), 2, DisplayType.Default, "",
		true)]
	public class AdaptiveMtRibbon : AbstractAction
	{
		public ProjectsController GetProjectsController()
		{
			return SdlTradosStudio.Application.GetController<ProjectsController>();
		}

		private Document ActiveDocument { get; set; }

		private static EditorController GetEditorController()
		{
			return SdlTradosStudio.Application.GetController<EditorController>();
		}

		protected override async void Execute()
		{
			var editorController = GetEditorController();
			var projects = GetProjectsController().SelectedProjects;

			var userCredentials = Helpers.Credentials.GetCredentials();
			if (userCredentials != null)
			{
				var userDetails = await ApiClient.Login(userCredentials.Email, userCredentials.Password);

				await ApiClient.OosSession(userCredentials, userDetails.Sid);

				var providerUrl = string.Empty;

				foreach (var project in projects)
				{
					var providerExist = false;
					var provider = project.GetTranslationProviderConfiguration();

					foreach (var entry in provider.Entries)
					{
						if (entry.MainTranslationProvider.Enabled &&
						    entry.MainTranslationProvider.Uri.AbsoluteUri.Contains("bmslanguagecloud"))
						{
							providerExist = true;
							providerUrl = HttpUtility.UrlDecode(HttpUtility.UrlDecode(entry.MainTranslationProvider.Uri.AbsoluteUri));
							break; //for the moment we take only the first cloud provider

						}
					}

					if (providerExist)
					{
						var files = project.GetTargetLanguageFiles();
						var providersDetails = EngineDetails.GetDetailsFromEngineUrl(providerUrl);

						foreach (var file in files)
						{
							var targetLanguage = file.Language.IsoAbbreviation;
							var document = editorController.Open(file, EditingMode.Translation);
							var segmentPairs = document.SegmentPairs.ToList();
							var providerDetails = providersDetails.FirstOrDefault(t => t.TargetLang.Equals(targetLanguage));
							if (providerDetails != null)
							{
								providerDetails.SourceLang = file.SourceFile.Language.IsoAbbreviation;

								//Confirm each segment
								foreach (var segmentPair in segmentPairs)
								{
									if (segmentPair.Target.ToString() != string.Empty)
									{

										var feedbackRequest = Helpers.Api.CreateFeedbackRequest(segmentPair, providerDetails);

										var feedbackReaponse = await ApiClient.Feedback(userDetails.Sid, feedbackRequest);
										if (feedbackReaponse.Success)
										{
											segmentPair.Properties.ConfirmationLevel = ConfirmationLevel.Translated;
											editorController.ActiveDocument.UpdateSegmentPairProperties(segmentPair, segmentPair.Properties);
										}

									}
								}
							}
							project.Save();
						}
				
					}

				}
			}
		}
	}
}
