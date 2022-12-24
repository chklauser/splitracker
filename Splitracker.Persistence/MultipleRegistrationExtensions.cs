using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Splitracker.Persistence;

static class MultipleRegistrationExtensions
{
    internal enum Scope
    {
        Singleton,
        Scoped,
        Transient,
    }

    public static MultipleRegistrations<TImplementation> AddTransientImplementation<TImplementation>(
        this IServiceCollection serviceCollection
    )
    where TImplementation: class
    {
        serviceCollection.AddTransient<TImplementation>();
        return new MultipleRegistrations<TImplementation>(serviceCollection, Scope.Transient);
    }
    
    public static MultipleRegistrations<TImplementation> AddScopedImplementation<TImplementation>(
        this IServiceCollection serviceCollection
    )
    where TImplementation: class
    {
        serviceCollection.AddScoped<TImplementation>();
        return new MultipleRegistrations<TImplementation>(serviceCollection, Scope.Scoped);
    }
    
    public static MultipleRegistrations<TImplementation> AddSingletonImplementation<TImplementation>(
        this IServiceCollection serviceCollection
    )
    where TImplementation: class
    {
        serviceCollection.AddSingleton<TImplementation>();
        return new MultipleRegistrations<TImplementation>(serviceCollection, Scope.Singleton);
    }
    
    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    public readonly struct MultipleRegistrations<TImplementation> where TImplementation : class
    {
        internal readonly IServiceCollection Services;
        internal readonly Scope Scope;

        internal MultipleRegistrations(IServiceCollection services, Scope scope)
        {
            Services = services;
            Scope = scope;
        }

        [MustUseReturnValue("Must call AsWell or AsWellAs")]
        public MultipleRegistrations<TInterface, TImplementation> As<TInterface>()
            where TInterface : class
        {
            return new(this);
        }
    }

    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    public readonly struct MultipleRegistrations<TInterface, TImplementation>
        where TImplementation : class
        where TInterface : class
    {
        internal readonly MultipleRegistrations<TImplementation> Inner;

        internal MultipleRegistrations(MultipleRegistrations<TImplementation> inner)
        {
            Inner = inner;
        }
    }

    public static void AsWell<TInterface, TImplementation>(this MultipleRegistrations<TInterface, TImplementation> self) 
        where TInterface : class 
        where TImplementation : class, TInterface
    {
        switch (self.Inner.Scope)
        {
            case Scope.Singleton:
                self.Inner.Services.AddSingleton<TInterface, TImplementation>();
                break;
            case Scope.Scoped:
                self.Inner.Services.AddScoped<TInterface, TImplementation>();
                break;
            case Scope.Transient:
                self.Inner.Services.AddTransient<TInterface, TImplementation>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(self.Inner.Scope), self.Inner.Scope, "Unknown scope type.");
        }
    }

    public static MultipleRegistrations<TImplementation> AsWellAnd<TInterface, TImplementation>(this MultipleRegistrations<TInterface, TImplementation> self) 
        where TInterface : class 
        where TImplementation : class, TInterface
    {
        AsWell(self);
        return self.Inner;
    }

    public static MultipleRegistrations<TImplementation> AsHostedService<TImplementation>(this MultipleRegistrations<TImplementation> self)
        where TImplementation : class, IHostedService
    {
        switch (self.Scope)
        {
            case Scope.Singleton:
                self.Services.AddHostedService<TImplementation>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(self.Scope), self.Scope, "Hosted services must be singletons");
        }
        return self;
    }
}