using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRasta.DI;
using OpenRasta.Hosting;
using OpenRasta.Hosting.InMemory;
using OpenRasta.Pipeline;
using OpenRasta.Tests.Unit.DI;
using OpenRasta.Tests.Unit.Fakes;
using Shouldly;

namespace InternalDependencyResolver_Specification
{
  public abstract class when_resolving_instances : dependency_resolver_context
  {
    public class TypeWithDependencyResolverAsProperty
    {
      public IDependencyResolver Resolver { get; set; }
    }

    public class TypeWithPropertyAlreadySet
    {
      public TypeWithPropertyAlreadySet()
      {
        Resolver = new InternalDependencyResolver();
      }

      public IDependencyResolver Resolver { get; set; }
    }

    [Test]
    public void a_property_that_would_cause_a_cyclic_dependency_is_ignored()
    {
      Resolver.AddDependency<RecursiveProperty>();

      Resolver.Resolve<RecursiveProperty>().Property.ShouldBeNull();
    }

    [Test]
    public void a_type_cannot_be_created_when_its_dependencies_are_not_registered()
    {
      Resolver.AddDependency<IAnother, Another>();

      Executing(() => Resolver.Resolve<IAnother>()).ShouldThrow<DependencyResolutionException>();
    }

    [Test]
    public void an_empty_enumeration_of_unregistered_types_is_resolved()
    {
      var simpleList = Resolver.ResolveAll<ISimple>();

      simpleList.ShouldNotBeNull();
      simpleList.ShouldBeEmpty();
    }

    [Test]
    public void a_type_can_get_a_dependency_resolver_dependency_assigned()
    {
      Resolver.AddDependencyInstance(typeof(IDependencyResolver), Resolver);
      Resolver.AddDependency<TypeWithDependencyResolverAsProperty>(DependencyLifetime.Transient);

      Resolver.Resolve<TypeWithDependencyResolverAsProperty>()
        .Resolver.ShouldBeSameAs(Resolver);
    }

    [Test]
    public void a_property_for_which_there_is_a_property_already_assigned_is_replaced_with_value_from_container()
    {
      Resolver.AddDependencyInstance(typeof(IDependencyResolver), Resolver);
      Resolver.AddDependency<TypeWithPropertyAlreadySet>(DependencyLifetime.Singleton);

      Resolver.Resolve<TypeWithPropertyAlreadySet>()
        .Resolver.ShouldBeSameAs(Resolver);
    }
  }

  public abstract class when_registering_for_per_request_lifetime : dependency_resolver_context
  {
    InMemoryContextStore InMemoryStore;

    public class TheClass
    {
    }


    public class TheClassThatNeedsYou
    {
      private readonly INeedYou _needYou;

      public TheClassThatNeedsYou(INeedYou needYou)
      {
        _needYou = needYou;
      }
    }

    public interface INeedYou
    {
    }

    public class NeedYou : INeedYou
    {
    }

    public class TheDependentClassThatNeedsYou
    {
      private readonly TheClassThatNeedsYou theClassThatNeedsYou;

      public TheDependentClassThatNeedsYou(TheClassThatNeedsYou theClassThatNeedsYou)
      {
        this.theClassThatNeedsYou = theClassThatNeedsYou;
      }
    }

    public class TheDependentClass
    {
      public TheDependentClass(TheClass dependent)
      {
        Dependent = dependent;
      }

      public TheClass Dependent { get; private set; }
    }

    void WhenClearingStore()
    {
      InMemoryStore.Clear();
    }

    void GivenInMemoryStore()
    {
      InMemoryStore = new InMemoryContextStore();
      Resolver.AddDependencyInstance<IContextStore>(InMemoryStore);
    }

    [Test]
    public void a_dependency_registered_in_one_context_is_not_registered_in_another()
    {
      var objectForScope1 = new TheClass();

      var scope1 = new AmbientContext();
      var scope2 = new AmbientContext();

      Resolver.AddDependency<IContextStore, AmbientContextStore>();

      using (new ContextScope(scope1))
      {
        Resolver.AddDependencyInstance<TheClass>(objectForScope1, DependencyLifetime.PerRequest);
      }

      using (new ContextScope(scope2))
      {
        Resolver.HasDependency(typeof(TheClass)).ShouldBeFalse();

        Executing(() => Resolver.Resolve<TheClass>()).ShouldThrow<DependencyResolutionException>();
      }
    }

    [Test]
    public void a_type_registered_as_per_request_cannot_be_resolved_if_IContextStore_is_not_registered()
    {
      Resolver.AddDependency<ISimple, Simple>(DependencyLifetime.PerRequest);

      Executing(() => Resolver.Resolve<ISimple>()).ShouldThrow<DependencyResolutionException>();
    }

    [Test]
    public void a_type_registered_as_transient_gets_an_instance_stored_in_context_injected()
    {
      Resolver.AddDependency<IContextStore, AmbientContextStore>();
      var objectForScope = new TheClass();
      var scope = new AmbientContext();
      Resolver.AddDependency<TheDependentClass>(DependencyLifetime.Transient);

      using (new ContextScope(scope))
      {
        Resolver.AddDependencyInstance(typeof(TheClass), objectForScope, DependencyLifetime.PerRequest);

        var dependentClass = Resolver.Resolve<TheDependentClass>();
        dependentClass.ShouldNotBeNull();
        dependentClass.Dependent.ShouldBeSameAs(objectForScope);
      }
    }

    [Test]
    public virtual void
      a_type_registered_as_transient_gets_an_instance_which_is_created_with_another_instance_and_is_registered_as_perwebrequest()
    {
      Resolver.AddDependency<IContextStore, AmbientContextStore>();

      Resolver.AddDependency<TheDependentClassThatNeedsYou>(DependencyLifetime.Transient);

      var contextStore = new AmbientContext();
      using (new ContextScope(contextStore))
      {
        var objectForScope = new TheClassThatNeedsYou(new NeedYou());

        Resolver.AddDependencyInstance(typeof(TheClassThatNeedsYou), objectForScope, DependencyLifetime.PerRequest);

        var dependentClass = Resolver.Resolve<TheDependentClassThatNeedsYou>();
        dependentClass.ShouldNotBeNull();
      }
    }

    [Test]
    public void non_instance_registrations_are_created_for_each_context_store()
    {
      GivenInMemoryStore();

      Resolver.AddDependency<Customer>(DependencyLifetime.PerRequest);

      var instance = Resolver.Resolve<Customer>();
      var anotherInstanceInSameContext = Resolver.Resolve<Customer>();

      instance.ShouldBeSameAs(anotherInstanceInSameContext);

      WhenClearingStore();

      var instance2 = Resolver.Resolve<Customer>();

      instance.ShouldNotBeSameAs(instance2);
    }

    [Test]
    public void registering_instances_in_different_scopes_results_in_each_consumer_getting_the_correct_registration()
    {
      var objectForScope1 = new TheClass();
      var objectForScope2 = new TheClass();
      var scope1 = new AmbientContext();
      var scope2 = new AmbientContext();

      Resolver.AddDependency<IContextStore, AmbientContextStore>();

      using (new ContextScope(scope1))
        Resolver.AddDependencyInstance<TheClass>(objectForScope1, DependencyLifetime.PerRequest);

      using (new ContextScope(scope2))
        Resolver.AddDependencyInstance<TheClass>(objectForScope2, DependencyLifetime.PerRequest);

      using (new ContextScope(scope1))
      {
        Resolver.Resolve<TheClass>().ShouldBeSameAs(objectForScope1);
      }
    }

    [Test]
    public void
      registering_instances_in_different_scopes_results_in_only_the_context_specific_registrations_to_be_resolved_in_a_context()
    {
      var objectForScope1 = new TheClass();
      var objectForScope2 = new TheClass();
      var scope1 = new AmbientContext();
      var scope2 = new AmbientContext();

      Resolver.AddDependency<IContextStore, AmbientContextStore>();

      using (new ContextScope(scope1))
        Resolver.AddDependencyInstance<TheClass>(objectForScope1, DependencyLifetime.PerRequest);

      using (new ContextScope(scope2))
        Resolver.AddDependencyInstance<TheClass>(objectForScope2, DependencyLifetime.PerRequest);

      using (new ContextScope(scope1))
      {
        Resolver.ResolveAll<TheClass>()
          .ShouldBe(new[]{objectForScope1});
      }
    }

    [Test]
    public void registering_two_instances_for_the_same_type_resolves_at_least_one_entry()
    {
      GivenInMemoryStore();
      var firstInstance = new TheClass();
      var secondInstance = new TheClass();

      Resolver.AddDependencyInstance<TheClass>(firstInstance, DependencyLifetime.PerRequest);
      Resolver.AddDependencyInstance<TheClass>(secondInstance, DependencyLifetime.PerRequest);

      var result = Resolver.ResolveAll<TheClass>();

      (result.Contains(firstInstance) || result.Contains(secondInstance)).ShouldBeTrue();
    }

    [Test]
    public void per_request_creates_new_instance_in_between_requests()
    {
      InMemoryStore = new InMemoryContextStore();
      Resolver.AddDependencyInstance<IContextStore>(InMemoryStore);

      Resolver.AddDependency<IUnknown, JohnDoe>(DependencyLifetime.PerRequest);

      var firstInstance = Resolver.Resolve<IUnknown>();


      Resolver.HandleIncomingRequestProcessed();
      InMemoryStore.Clear();
      var secondInstance = Resolver.Resolve<IUnknown>();

      firstInstance.ShouldNotBeSameAs(secondInstance);
    }

    interface IUnknown
    {
    }

    class JohnDoe : IUnknown
    {
    }

    [Test]
    public void instance_registered_as_per_request_are_cleared_when_context_store_is_terminating()
    {
      GivenInMemoryStore();
      var firstInstance = new TheClass();

      Resolver.AddDependencyInstance<TheClass>(firstInstance, DependencyLifetime.PerRequest);

      Resolver.Resolve<TheClass>().ShouldBeSameAs(firstInstance);

      Resolver.HandleIncomingRequestProcessed();

      Executing(() => Resolver.Resolve<TheClass>()).ShouldThrow<DependencyResolutionException>();
    }
  }


  public abstract class when_registering_dependencies : dependency_resolver_context
  {
    [Test]
    public void an_abstract_type_cannot_be_registered()
    {
      Executing(() => Resolver.AddDependency<ISimple, SimpleAbstract>()).ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void an_interface_cannot_be_registered_as_a_concrete_implementation()
    {
      Executing(() => Resolver.AddDependency<ISimple, IAnotherSimple>()).ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void cyclic_dependency_generates_an_error()
    {
      Resolver.AddDependency<RecursiveConstructor>();

      Executing(() => Resolver.Resolve<RecursiveConstructor>()).ShouldThrow<DependencyResolutionException>();
    }

    [Test]
    public void parameters_are_resolved()
    {
      Resolver.AddDependency<ISimple, Simple>();
      Resolver.AddDependency<ISimpleChild, SimpleChild>();

      var prop = ((Simple) Resolver.Resolve<ISimple>()).Property;
      prop.ShouldNotBeNull();
      prop.ShouldBeAssignableTo<SimpleChild>();
    }

    [Test]
    public void registered_concrete_type_is_recognized_as_dependency_implementation()
    {
      Resolver.AddDependency<ISimple, Simple>();

      Resolver.HasDependencyImplementation(typeof(ISimple), typeof(Simple)).ShouldBeTrue();
    }

    [Test]
    public void registering_a_concrete_type_results_in_the_type_being_registered()
    {
      Resolver.AddDependency(typeof(Simple), DependencyLifetime.Transient);
      Resolver.HasDependency(typeof(Simple)).ShouldBeTrue();
    }

    [Test]
    public void registering_a_concrete_type_with_an_unknown_dependency_lifetime_value_results_in__an_error()
    {
      Executing(() => Resolver.AddDependency<Simple>((DependencyLifetime) 999))
        .ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void registering_a_service_type_with_an_unknown_dependency_lifetime_value_results_in__an_error()
    {
      Executing(() => Resolver.AddDependency<ISimple, Simple>((DependencyLifetime) 999))
        .ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void requesting_a_type_with_a_public_constructor_returns_a_new_instance_of_that_type()
    {
      Resolver.AddDependency<ISimple, Simple>();
      Resolver.Resolve<ISimple>().ShouldBeAssignableTo<Simple>();
    }

    [Test]
    public void requesting_a_type_with_no_public_constructor_will_return_a_type_with_the_correct_dependency()
    {
      Resolver.AddDependency<ISimple, Simple>();
      Resolver.AddDependency<IAnother, Another>();

      ((Another) Resolver.Resolve<IAnother>()).Dependent.ShouldBeAssignableTo<Simple>();
    }

    [Test]
    public void the_constructor_with_the_most_matching_arguments_is_used()
    {
      Resolver.AddDependency<ISimple, Simple>();
      Resolver.AddDependency<IAnother, Another>();
      Resolver.AddDependency<TypeWithTwoConstructors>();

      var type = Resolver.Resolve<TypeWithTwoConstructors>();
      type._argOne.ShouldNotBeNull();
      type._argTwo.ShouldNotBeNull();
    }

    [Test]
    public void the_null_value_is_never_registered()
    {
      Resolver.HasDependency(null).ShouldBeFalse();
    }

    [Test]
    public void the_resolved_instance_is_the_same_as_the_registered_instance()
    {
      var objectInstance = new object();

      Resolver.AddDependencyInstance(typeof(object), objectInstance);

      Resolver.Resolve<object>().ShouldBe(objectInstance);
      //return valueToAnalyse;
    }
  }
}