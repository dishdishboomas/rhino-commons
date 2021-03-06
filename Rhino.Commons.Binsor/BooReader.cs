﻿#region license

// Copyright (c) 2005 - 2007 Ayende Rahien (ayende@ayende.com)
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//     this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice,
//     this list of conditions and the following disclaimer in the documentation
//     and/or other materials provided with the distribution.
//     * Neither the name of Ayende Rahien nor the names of its
//     contributors may be used to endorse or promote products derived from this
//     software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
// THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using Boo.Lang.Compiler;
using Boo.Lang.Compiler.IO;
using Boo.Lang.Compiler.Pipelines;
using Boo.Lang.Compiler.Steps;
using Boo.Lang.Extensions;
using Boo.Lang.Parser;
using Castle.MicroKernel;
using Castle.MicroKernel.SubSystems.Resource;
using Castle.Core.Resource;
using Castle.Windsor;

namespace Rhino.Commons.Binsor
{
	using System.Reflection;
	using DSL;

	public static class BooReader
	{
		private static readonly BooToken tokenThatIsNeededToKeepReferenceToTheBooParserAssembly = new BooToken();

		public static AbstractConfigurationRunner Read(IWindsorContainer container, CustomUri uri, string name, params string[] namespaces)
        {
            return Read(container, uri, name, name, namespaces);
        }

		public static AbstractConfigurationRunner Read(IWindsorContainer container, string fileName, params string[] namespaces)
		{
			return Read(container, fileName, "", namespaces);
		}

		public static AbstractConfigurationRunner Read(IWindsorContainer container, string fileName, string environment, params string[] namespaces)
		{
			return Read(container, fileName, environment, GenerationOptions.Memory, namespaces);
		}

		public static AbstractConfigurationRunner Read(
			IWindsorContainer container, string fileName, string environment,
			GenerationOptions generationOptions, params string[] namespaces)
		{
			var conf = GetConfigurationInstanceFromFile(fileName, environment, container, generationOptions, namespaces);
			Execute(container, conf);
			return conf;
		}

		public static AbstractConfigurationRunner Read(
            IWindsorContainer container, CustomUri uri,
            GenerationOptions generationOptions, string name,
            string environment, params string[] namespaces)
        {
            using (AbstractConfigurationRunner.UseLocalContainer(container))
            {
				var conf = GetConfigurationInstanceFromResource(name, environment, container, uri, generationOptions, namespaces);
				conf.Run();
				return conf;
            }
        }

		public static void Execute(IWindsorContainer container, AbstractConfigurationRunner abstractConfiguration)
		{
			Execute(container, () => abstractConfiguration.Run());
		}

		public static void Execute(IWindsorContainer container, Action registrations)
		{
			using (AbstractConfigurationRunner.UseLocalContainer(container))
			{
				registrations();
			}
		}


		public static AbstractConfigurationRunner Read(IWindsorContainer container, Stream stream, string name, params string[] namespaces)
		{
			return Read(container, stream, name, "", namespaces);
		}

		public static AbstractConfigurationRunner Read(
			IWindsorContainer container, Stream stream,
			string name, string environment, params string[] namespaces)
		{
			return Read(container, stream, GenerationOptions.Memory, name, environment, namespaces);
		}

		public static AbstractConfigurationRunner Read(
            IWindsorContainer container, CustomUri uri,
            string name, string environment, params string[] namespaces)
        {
            return Read(container, uri, GenerationOptions.Memory, name, environment, namespaces);
        }

		public static AbstractConfigurationRunner Read(
			IWindsorContainer container, Stream stream,
			GenerationOptions generationOptions, string name,
			string environment, params string[] namespaces)
		{
			using (AbstractConfigurationRunner.UseLocalContainer(container))
			{
				var conf = GetConfigurationInstanceFromStream(name, environment, container, stream, generationOptions, namespaces);
				conf.Run();
				return conf;
			}
		}

		public static AbstractConfigurationRunner GetConfigurationInstanceFromFile(
			string fileName, string environment, IWindsorContainer container,
			GenerationOptions generationOptions, params string[] namespaces)
		{
			string baseDirectory = Path.GetDirectoryName(fileName);
			var urlResolver = CreateWindorUrlResolver(container);
			using (var reader = urlResolver(fileName, null))
			{
				return GetConfigurationInstance(
					Path.GetFileNameWithoutExtension(fileName), environment,
					new ReaderInput(Path.GetFileNameWithoutExtension(fileName), reader),
					generationOptions,
					new AutoReferenceFilesCompilerStep(baseDirectory, urlResolver),
					namespaces);
			}
		}

	    public static AbstractConfigurationRunner GetConfigurationInstanceFromResource(
            string name, string environment, IWindsorContainer container, CustomUri uri,
			GenerationOptions generationOptions, params string[] namespaces)
	    {
			var system = (IResourceSubSystem)container.Kernel.GetSubSystem(SubSystemConstants.ResourceKey);
			var resource = system.CreateResource(uri);
            string baseDirectory = Path.GetDirectoryName(uri.Path);
			var urlResolver = CreateWindorUrlResolver(container);
                return GetConfigurationInstance(
                    name, environment, new ReaderInput(name, resource.GetStreamReader()),
                    generationOptions, new AutoReferenceFilesCompilerStep(baseDirectory,urlResolver),
                    namespaces);
	    }
		public static AbstractConfigurationRunner GetConfigurationInstanceFromStream(
			string name, string environment, IWindsorContainer container, Stream stream,
			GenerationOptions generationOptions, params string[] namespaces)
		{
			var urlResolver = CreateWindorUrlResolver(container);
			using (var reader = new StreamReader(stream))
			{
				return GetConfigurationInstance(
					name, environment, new ReaderInput(name, reader),
					generationOptions, new AutoReferenceFilesCompilerStep(urlResolver),
					namespaces);
			}
		}

		private static AbstractConfigurationRunner GetConfigurationInstance(
			string name, string environment, ICompilerInput input,
			GenerationOptions generationOptions, ICompilerStep autoReferenceStep,
			params string[] namespaces)
		{
			var compiler = new BooCompiler();
			compiler.Parameters.Ducky = true;
			compiler.Parameters.Pipeline =
				generationOptions == GenerationOptions.Memory ?
				new CompileToMemory() : 
				new CompileToFile();

			compiler.Parameters.Pipeline.Insert(1, autoReferenceStep);
			compiler.Parameters.Pipeline.Insert(2, new BinsorCompilerStep(environment, namespaces));
			compiler.Parameters.Pipeline.Replace(
				typeof (ProcessMethodBodiesWithDuckTyping),
				new TransformUnknownReferences());
			compiler.Parameters.Pipeline.InsertAfter(typeof (TransformUnknownReferences),
			                                         new RegisterAfterCreation());

			compiler.Parameters.OutputType = CompilerOutputType.Library;
			compiler.Parameters.Input.Add(input);
			compiler.Parameters.References.Add(typeof (BooReader).Assembly);
			compiler.Parameters.References.Add(typeof (MacroMacro).Assembly);

			TryAddAssembliesReferences(compiler.Parameters, "Rhino.Commons.NHibernate", "Rhino.Commons.ActiveRecord");

			var run = compiler.Run();
			if (run.Errors.Count != 0)
			{
				throw new CompilerError(string.Format("Could not compile configuration! {0}", run.Errors.ToString(true)));
			}
			var type = run.GeneratedAssembly.GetType(name.Replace('.', '_'));
			return Activator.CreateInstance(type) as AbstractConfigurationRunner;
		}

		/// <summary>
		/// Tries the add the speicifed assemblies to the compiler's references
		/// In a late bound way
		/// </summary>
		/// <param name="cp">The compiler parameters.</param>
		/// <param name="assembliesToAdd">The assemblies to add.</param>
		private static void TryAddAssembliesReferences(CompilerParameters cp, params string[] assembliesToAdd)
		{
			foreach (string assembly in assembliesToAdd)
			{
				try
				{
					cp.References.Add(Assembly.Load(assembly));
				}
				catch
				{
					// we don't worry if we can't load the assemblies
				}
			}
		}

		private static UrlResolverDelegate CreateWindorUrlResolver(IWindsorContainer container)
		{
			var subSystem = (IResourceSubSystem) container.Kernel.GetSubSystem(SubSystemConstants.ResourceKey);

			return (url, basePath) =>
			{
				if (url.IndexOf(':') < 0)
				{
					url = "file://" + url;
				}

				var resource = string.IsNullOrEmpty(basePath)
					? subSystem.CreateResource(url) 
					: subSystem.CreateResource(url, basePath);

				using (resource)
				using (var reader = resource.GetStreamReader())
					return new StringReader(reader.ReadToEnd());
			};
		}

		public enum GenerationOptions
		{
			Memory,
			File
		}
	}
}
