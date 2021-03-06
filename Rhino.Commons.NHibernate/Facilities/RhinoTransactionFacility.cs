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

using System.Linq;
using Castle.Core;
using Castle.Facilities.AutoTx;
using Castle.Services.Transaction;
using Rhino.Commons.Facilities;
using Castle.MicroKernel.Registration;

namespace Rhino.Commons
{
	public class RhinoTransactionFacility : TransactionFacility
	{
		protected override void Init()
		{
			base.Init();//set the inspector for the transactional components
			Kernel.ComponentCreated += Kernel_ComponentCreated;
			SetUpTransactionManager();
		}

		private void SetUpTransactionManager()
		{
			if (!Kernel.HasComponent(typeof(ITransactionManager)))
			{
				Kernel.Register(Component.For<ITransactionManager>().ImplementedBy<DefaultTransactionManager>().Named("rhino.transaction.manager"));
			}
		}

		private void OnNewTransaction(object sender, TransactionEventArgs args)
		{
			if (!args.Transaction.IsAmbient)
			{
				args.Transaction.Enlist(new RhinoTransactionResourceAdapter(args.Transaction.TransactionMode));
			}
		}

		private void Kernel_ComponentCreated(ComponentModel model, object instance)
		{
			if (model.Services.Any(service => service == typeof(ITransactionManager)))
			{
				var txMgr = (ITransactionManager)instance;
				txMgr.TransactionCreated += OnNewTransaction;
			}	
		}
	}
}
