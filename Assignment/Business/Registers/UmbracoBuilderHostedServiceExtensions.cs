using Assignment.Business.Schedule_jobs;

namespace Assignment.Business.Registers
{
	public static class UmbracoBuilderHostedServiceExtensions
	{
			public static IUmbracoBuilder AddCustomHostedServices(this IUmbracoBuilder
			builder)
			{
				builder.Services.AddHostedService<TTImport>();
				return builder;
			}
	}
}
