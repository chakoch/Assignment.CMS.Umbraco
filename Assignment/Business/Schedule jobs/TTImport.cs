using Assignment.Business.Models;
using Newtonsoft.Json;
using System.Net;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Assignment.Business.Schedule_jobs
{
	public class TTImport : RecurringHostedServiceBase
	{
		private readonly IRuntimeState _runtimeState;
		private readonly IContentService _contentService;
		private readonly ILogger<TTImport> _logger;
		private readonly ICoreScopeProvider _scopeProvider;
		private readonly IUmbracoContextFactory _umbracoContextFactory;
		private static TimeSpan HowOftenWeRepeat => TimeSpan.FromMinutes(2);
		private static TimeSpan DelayBeforeWeStart => TimeSpan.FromMinutes(1);
		public TTImport(
		IRuntimeState runtimeState,
		IContentService contentService,
		ILogger<TTImport> logger,
		IUmbracoContextFactory umbracoContextFactory,
		ICoreScopeProvider scopeProvider)
		: base(logger, HowOftenWeRepeat, DelayBeforeWeStart)
		{
			_runtimeState = runtimeState;
			_contentService = contentService;
			_logger = logger;
			_umbracoContextFactory = umbracoContextFactory;
			_scopeProvider = scopeProvider;
		}
		public override Task PerformExecuteAsync(object? state)
		{
			// Don't do anything if the site is not running.
			if (_runtimeState.Level is not RuntimeLevel.Run)
			{
				return Task.CompletedTask;
			}
			// Wrap the three content service calls in a scope to do it all in one transaction.
			using ICoreScope scope = _scopeProvider.CreateCoreScope();
			_logger.LogInformation("Start TT Nyheter Import job");
			var homePage = _contentService.GetRootContent().First();
			IPublishedContent BloggLista = null;
			IEnumerable<IPublishedContent>? children = null;
			using (var umbracoContextReference =
			_umbracoContextFactory.EnsureUmbracoContext())
			{
				var content =
				umbracoContextReference?.UmbracoContext?.Content?.GetById(homePage.Id);
				if (content != null)
				{
					BloggLista = content.ChildrenOfType("PressReleases").FirstOrDefault();
					//BloggLista = content;
					_logger.LogInformation("Running tt import job " + BloggLista.Name + " " +
					BloggLista.Id);
				}
				if (BloggLista != null)
				{
					var parentId = Guid.Parse(BloggLista.Key.ToString());
					children = BloggLista.Children;
					var json = HamtaData().Take(5);
					foreach (var item in json)
					{
						bool hasMatch = children != null ? children.Any(x => x.Name?.ToLower() ==
						item.Title.ToLower()) : true;
						if (!hasMatch)
						{
							// Create a new child item of type 'Product'
							var demoproduct = _contentService.Create(item.Title, parentId,
							"PressItem");
							// Set the value of the property with alias 'category'
							demoproduct.SetValue("title", item.Title);
							// Set the value of the property with alias 'price'
							demoproduct.SetValue("LeadText", item.LeadText);
							demoproduct.SetValue("PressItemId", item.Id);
							_contentService.SaveAndPublish(demoproduct);
						}
					}
				}
				_logger.LogInformation("End TT nyheter Import job");
			}
			// Remember to complete the scope when done.
			scope.Complete();
			return Task.CompletedTask;
		}
		private IList<TTModel> HamtaData()
		{
			var feedUrl = new
			Uri("https://via.tt.se/json/v2/releases?publisher=686463&channels=696961");
			string jsonData;
			System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls |
			SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			using (var httpClient = new HttpClient())
			{
				var response =
				httpClient.GetAsync(feedUrl).ConfigureAwait(false).GetAwaiter().GetResult(); ;
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogInformation("TT nyheter Respons fel");
					return null;
				}
				var ttData =
				response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
				try
				{
					var ttRootObject = JsonConvert.DeserializeObject<RootModel>(ttData);
					return ttRootObject.Releases;
				}
				catch (JsonSerializationException jse)
				{
					_logger.LogInformation("TT nyheter deserialize error");
					return null;
				}
				catch (Exception e)
				{
					_logger.LogInformation("You have {e} items to clean", e);
					return null;
				}
			}
		}
	}
}
