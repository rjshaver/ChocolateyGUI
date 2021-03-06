﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Hacks.cs" company="Chocolatey">
//   Copyright 2014 - Present Rob Reynolds, the maintainers of Chocolatey, and RealDimensions Software, LLC
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using chocolatey;
using chocolatey.infrastructure.app.configuration;
using chocolatey.infrastructure.app.services;
using chocolatey.infrastructure.registration;

namespace ChocolateyGui
{
    public static class Hacks
    {
        private static readonly ConcurrentDictionary<Type, Func<object>> InstanceExpCache = new ConcurrentDictionary<Type, Func<object>>();

        public static bool IsElevated => (WindowsIdentity.GetCurrent().Owner?.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid)).GetValueOrDefault(false);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "N/A")]
        public static IChocolateyPackageInformationService GetPackageInformationService()
        {
            return GetInstance<IChocolateyPackageInformationService>();
        }

        public static ChocolateyConfiguration GetConfiguration(this GetChocolatey getChocolatey)
        {
            var getParam = Expression.Parameter(typeof(GetChocolatey), "getChocolatey");
            var args = Expression.Constant(new List<string>());
            var castedArgs = Expression.Convert(args, typeof(IList<string>));
            var getInstanceMethod = typeof(GetChocolatey)
                  .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                  .First(mi => mi.Name == "create_configuration");

            var call = Expression.Call(getParam, getInstanceMethod, castedArgs);
            var func = Expression.Lambda<Func<GetChocolatey, ChocolateyConfiguration>>(call, getParam).Compile();
            return func(getChocolatey);
        }

        public static T GetInstance<T>()
        {
            Func<object> instanceExp;
            if (!InstanceExpCache.TryGetValue(typeof(T), out instanceExp))
            {
                instanceExp = GetInstanceExp<T>();
                InstanceExpCache.TryAdd(typeof(T), instanceExp);
            }

            return (T)instanceExp();
        }

        private static ConstantExpression GetContainerExp()
        {
            return Expression.Constant(SimpleInjectorContainer.Container);
        }

        private static Func<object> GetInstanceExp<T>()
        {
            var containerType = SimpleInjectorContainer.Container.GetType();
            var containerParam = GetContainerExp();
            var getInstanceMethod = containerType
                  .GetMethods()
                  .Where(m => m.Name == "GetInstance")
                  .Select(m => new
                  {
                      Method = m,
                      Params = m.GetParameters(),
                      Args = m.GetGenericArguments()
                  })
                  .Where(x => x.Params.Length == 0
                              && x.Args.Length == 1)
                  .Select(x => x.Method)
                  .First();
            var block = Expression.Block(
                Expression.Convert(
                    Expression.Call(containerParam, getInstanceMethod.MakeGenericMethod(typeof(T))),
                    typeof(object)));
            var func = Expression.Lambda<Func<object>>(block).Compile();
            return func;
        }
    }
}