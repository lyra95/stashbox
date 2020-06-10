﻿using Stashbox.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Stashbox.Tests
{

    public class ReMapTests
    {
        [Fact]
        public void ReMapTests_Replace_SingleResolve()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test1>(context => context.WithName("teszt"));
            container.Register<ITest1, Test12>(context => context.WithName("teszt2"));

            var test1 = container.Resolve<ITest1>("teszt");
            var test2 = container.Resolve<ITest1>("teszt2");

            Assert.IsType<Test1>(test1);
            Assert.IsType<Test12>(test2);

            container.Register<ITest1, Test11>(context => context.WithName("teszt").ReplaceExisting());

            var test11 = container.Resolve<ITest1>("teszt");
            var test12 = container.Resolve<ITest1>("teszt2");

            Assert.IsType<Test11>(test11);
            Assert.IsType<Test12>(test12);
        }

        [Fact]
        public void ReMapTests_Replace_Enumerable_Named()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test1>(context => context.WithName("teszt"));
            container.Register<ITest1, Test12>(context => context.WithName("teszt2"));

            var coll = container.Resolve<IEnumerable<ITest1>>().ToArray();

            Assert.IsType<Test1>(coll[0]);
            Assert.IsType<Test12>(coll[1]);

            container.Register<ITest1, Test11>(context => context.WithName("teszt").ReplaceExisting());

            var coll2 = container.Resolve<IEnumerable<ITest1>>().ToArray();

            Assert.IsType<Test11>(coll2[0]);
            Assert.IsType<Test12>(coll2[1]);
        }

        [Fact]
        public void ReMapTests_Enumerable_Named()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test1>(context => context.WithName("teszt"));
            container.Register<ITest1, Test12>(context => context.WithName("teszt2"));

            var coll = container.Resolve<IEnumerable<ITest1>>().ToArray();

            Assert.Equal(2, coll.Length);
            Assert.IsType<Test1>(coll[0]);
            Assert.IsType<Test12>(coll[1]);

            container.ReMap<ITest1, Test11>();

            var coll2 = container.Resolve<IEnumerable<ITest1>>().ToArray();

            Assert.Single(coll2);
            Assert.IsType<Test11>(coll2[0]);
        }

        [Fact]
        public void ReMapTests_Func_Named()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test12>(context => context.WithName("teszt2"));
            container.Register<ITest1, Test1>(context => context.WithName("teszt"));

            container.Resolve<Func<ITest1>>("teszt2");

            var func = container.Resolve<Func<ITest1>>("teszt");

            Assert.IsType<Test1>(func());

            container.ReMap<ITest1, Test11>(context => context.WithName("teszt"));

            var func2 = container.Resolve<Func<ITest1>>("teszt");

            Assert.IsType<Test11>(func2());
        }

        [Fact]
        public void ReMapTests_Lazy_Named()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test12>(context => context.WithName("teszt2"));
            container.Register<ITest1, Test1>(context => context.WithName("teszt"));

            container.Resolve<Lazy<ITest1>>("teszt");

            var lazy = container.Resolve<Lazy<ITest1>>("teszt");

            Assert.IsType<Test1>(lazy.Value);

            container.ReMap<ITest1, Test11>(config => config.WithName("teszt"));

            var lazy2 = container.Resolve<Lazy<ITest1>>("teszt");

            Assert.IsType<Test11>(lazy2.Value);
        }

        [Fact]
        public void ReMapTests_Enumerable()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test1>();

            var coll = container.Resolve<IEnumerable<ITest1>>().ToArray();

            Assert.IsType<Test1>(coll[0]);

            container.ReMap<ITest1, Test11>();

            var coll2 = container.Resolve<IEnumerable<ITest1>>().ToArray();

            Assert.IsType<Test11>(coll2[0]);
        }

        [Fact]
        public void ReMapTests_Func()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test1>();

            var func = container.Resolve<Func<ITest1>>();

            Assert.IsType<Test1>(func());

            container.ReMap<ITest1, Test11>();

            var func2 = container.Resolve<Func<ITest1>>();

            Assert.IsType<Test11>(func2());
        }

        [Fact]
        public void ReMapTests_Lazy()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test1>();

            var lazy = container.Resolve<Lazy<ITest1>>();

            Assert.IsType<Test1>(lazy.Value);

            container.ReMap<ITest1, Test11>();

            var lazy2 = container.Resolve<Lazy<ITest1>>();

            Assert.IsType<Test11>(lazy2.Value);
        }

        [Fact]
        public void ReMapTests_DependencyResolve()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1, Test1>();
            container.Register<ITest2, Test2>();

            var test2 = container.Resolve<ITest2>();

            Assert.NotNull(test2.Test1);
            Assert.IsType<Test1>(test2.Test1);

            container.ReMap<ITest1>(typeof(Test11));

            var test22 = container.Resolve<ITest2>();

            Assert.NotNull(test22.Test1);
            Assert.IsType<Test11>(test22.Test1);
        }

        [Fact]
        public void ReMapTests_DependencyResolve_WithoutService()
        {
            IStashboxContainer container = new StashboxContainer();
            container.RegisterSingleton<Test11>();
            container.Register<Test3>();

            var inst = container.Resolve<Test3>();

            var dep = inst.Test1;

            Assert.NotNull(dep);
            Assert.IsType<Test11>(dep);

            container.ReMap<Test11>();

            inst = container.Resolve<Test3>();

            Assert.NotNull(inst.Test1);
            Assert.IsType<Test11>(inst.Test1);

            Assert.NotSame(dep, inst.Test1);
        }

        [Fact]
        public void ReMapTests_DependencyResolve_Fluent()
        {
            IStashboxContainer container = new StashboxContainer();
            container.Register<ITest1>(typeof(Test1));
            container.Register<ITest2, Test2>();

            var test2 = container.Resolve<ITest2>();

            Assert.NotNull(test2.Test1);
            Assert.IsType<Test1>(test2.Test1);

            container.ReMap<ITest1>(typeof(Test11));

            var test22 = container.Resolve<ITest2>();

            Assert.NotNull(test22.Test1);
            Assert.IsType<Test11>(test22.Test1);
        }

        [Fact]
        public void ReMapTests_Throws_When_TypeMap_Invalid()
        {
            using var container = new StashboxContainer();
            Assert.Throws<InvalidRegistrationException>(() => container.ReMap<ITest1>(typeof(Test2)));
            Assert.Throws<InvalidRegistrationException>(() => container.ReMap(typeof(ITest1), typeof(Test2)));
            Assert.Throws<InvalidRegistrationException>(() => container.ReMap<ITest1>());
            Assert.Throws<InvalidRegistrationException>(() => container.ReMapDecorator(typeof(ITest1), typeof(Test2)));
        }

        interface ITest1 { }

        interface ITest2
        {
            ITest1 Test1 { get; }
        }

        class Test1 : ITest1
        { }

        class Test11 : ITest1
        { }

        class Test12 : ITest1
        { }

        class Test2 : ITest2
        {
            public ITest1 Test1 { get; }

            public Test2(ITest1 test1)
            {
                this.Test1 = test1;
            }
        }

        class Test3
        {
            public Test11 Test1 { get; }

            public Test3(Test11 test1)
            {
                this.Test1 = test1;
            }
        }
    }
}
