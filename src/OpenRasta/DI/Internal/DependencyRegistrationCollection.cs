using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenRasta.Pipeline;

namespace OpenRasta.DI.Internal
{
    public class DependencyRegistrationCollection : IContextStoreDependencyCleaner
    {
        readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, DependencyRegistration>> _registrations = new ConcurrentDictionary<Type, ConcurrentDictionary<string, DependencyRegistration>>();

        public IEnumerable<DependencyRegistration> this[Type serviceType] => GetSvcRegistrations(serviceType).Values;

        public void Add(DependencyRegistration registration)
        {
            registration.LifetimeManager.VerifyRegistration(registration);
            GetSvcRegistrations(registration.ServiceType).TryAdd(registration.Key, registration);
        }

        public DependencyRegistration GetRegistrationForService(Type type) =>
            GetSvcRegistrations(type).Values.LastOrDefault(x => x.LifetimeManager.IsRegistrationAvailable(x));

        public bool HasRegistrationForService(Type type) =>
            GetSvcRegistrations(type).Values.Any(x => x.LifetimeManager.IsRegistrationAvailable(x));

        public void Destruct(string key, object instance)
        {
            foreach (var reg in _registrations)
            {
                reg.Value.TryGetValue(key, out DependencyRegistration matchingRegistration);

                if (matchingRegistration?.IsInstanceRegistration == true)
                    reg.Value.TryRemove(key, out DependencyRegistration _);
            }
        }

        ConcurrentDictionary<string, DependencyRegistration> GetSvcRegistrations(Type serviceType) =>
            _registrations.GetOrAdd(serviceType, _ => new ConcurrentDictionary<string, DependencyRegistration>());
    }
}
