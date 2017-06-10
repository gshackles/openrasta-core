using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenRasta.Pipeline;

namespace OpenRasta.DI.Internal
{
    public class DependencyRegistrationCollection : IContextStoreDependencyCleaner
    {
        readonly ConcurrentDictionary<Type, List<DependencyRegistration>> _registrations = new ConcurrentDictionary<Type, List<DependencyRegistration>>();

        public IEnumerable<DependencyRegistration> this[Type serviceType] => GetSvcRegistrations(serviceType).ToList();

        public void Add(DependencyRegistration registration)
        {
            registration.LifetimeManager.VerifyRegistration(registration);
            GetSvcRegistrations(registration.ServiceType).Add(registration);
        }

        public DependencyRegistration GetRegistrationForService(Type type) =>
            GetSvcRegistrations(type).ToList().LastOrDefault(x => x.LifetimeManager.IsRegistrationAvailable(x));

        public bool HasRegistrationForService(Type type) =>
            GetSvcRegistrations(type).ToList().Any(x => x.LifetimeManager.IsRegistrationAvailable(x));

        public void Destruct(string key, object instance)
        {
            foreach (var reg in _registrations.Values)
            {
                var toRemove = reg.Where(x => x.IsInstanceRegistration && x.Key == key).ToList();

                toRemove.ForEach(x => reg.Remove(x));
            }
        }

        List<DependencyRegistration> GetSvcRegistrations(Type serviceType) =>
            _registrations.GetOrAdd(serviceType, _ => new List<DependencyRegistration>());
    }
}
