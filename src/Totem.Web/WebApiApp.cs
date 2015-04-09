﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autofac;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Logging;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Owin;
using Owin;
using Totem.Http;
using Totem.Runtime;
using Totem.Runtime.Map;

namespace Totem.Web
{
	/// <summary>
	/// An HTTP-bound API composed by OWIN and Nancy
	/// </summary>
	public abstract class WebApiApp : AutofacNancyBootstrapper, IWebApp, ITaggable
	{
		protected WebApiApp(WebAppContext context)
		{
			Context = context;
		}

		Tags ITaggable.Tags { get { return Tags; } }
		protected Tags Tags { get; private set; }
		protected IClock Clock { get { return Notion.Traits.Clock.Get(this); } }
		protected ILog Log { get { return Notion.Traits.Log.Get(this); } }
		protected RuntimeMap Runtime { get { return Notion.Traits.Runtime.Get(this); } }

		protected readonly WebAppContext Context;

		public virtual IDisposable Start()
		{
			return WebApp.Start(GetStartOptions(), Startup);
		}

		protected virtual StartOptions GetStartOptions()
		{
			var options = new StartOptions();

			foreach(var binding in Context.Bindings)
			{
				options.Urls.Add(binding.ToString());
			}

			return options;
		}

		protected virtual void Startup(IAppBuilder builder)
		{
			builder.UseNancy(GetNancyOptions());

			builder.SetLoggerFactory(new LogAdapter());
		}

		protected virtual NancyOptions GetNancyOptions()
		{
			return new NancyOptions { Bootstrapper = this };
		}

		//
		// Composition
		//

		protected override ILifetimeScope GetApplicationContainer()
		{
			return Context.Scope;
		}

		protected override abstract IEnumerable<INancyModule> GetAllModules(ILifetimeScope container);

		protected override INancyModule GetModule(ILifetimeScope container, Type moduleType)
		{
			return (INancyModule) container.Resolve(moduleType);
		}

		protected override void ConfigureRequestContainer(ILifetimeScope container, NancyContext context)
		{
			base.ConfigureRequestContainer(container, context);

			var module = new BuilderModule();

			module.Register(c => new WebApiCall(
				HttpLink.From(context.Request.Url.ToString()),
				HttpAuthorization.From(context.Request.Headers.Authorization),
				WebApiCallBody.From(context.Request.Headers.ContentType, () => context.Request.Body),
				c.Resolve<IViewDb>()))
			.InstancePerRequest();

			module.Update(container.ComponentRegistry);
		}

		//
		// Requests
		//

		protected override void RequestStartup(ILifetimeScope container, IPipelines pipelines, NancyContext context)
		{
			pipelines.BeforeRequest += pipelineContext => BeforeRequest(container, pipelineContext);

			pipelines.OnError += (pipelineContext, exception) => OnRequestError(container, pipelineContext, exception);
		}

		private Response BeforeRequest(ILifetimeScope container, NancyContext context)
		{
			SetCallItem(container, context);

			return context.Response;
		}

		private Response OnRequestError(ILifetimeScope container, NancyContext context, Exception exception)
		{
			return container.Resolve<IErrorHandler>().CreateResponse(context, exception);
		}

		private static void SetCallItem(ILifetimeScope container, NancyContext context)
		{
			context.Items[WebApi.CallItemKey] = container.Resolve<WebApiCall>();
		}

		private sealed class LogAdapter : Notion, ILoggerFactory, ILogger
		{
			public ILogger Create(string name)
			{
				return this;
			}

			public bool WriteCore(TraceEventType eventType, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
			{
				var level = GetLevel(eventType);

				// OwinHttpListener seems to always throw ObjectDisposedException when shutting down

				var canWrite = Log.IsAt(level) && !(exception is ObjectDisposedException);

				if(canWrite)
				{
					Log.At(level, Text.Of(() => "[web] " + formatter(state, exception)));
				}

				return canWrite;
			}

			private static LogLevel GetLevel(TraceEventType type)
			{
				switch(type)
				{
					case TraceEventType.Verbose:
						return LogLevel.Verbose;
					case TraceEventType.Warning:
						return LogLevel.Warning;
					case TraceEventType.Error:
						return LogLevel.Error;
					case TraceEventType.Critical:
						return LogLevel.Fatal;
					default:
						return LogLevel.Debug;
				}
			}
		}
	}
}