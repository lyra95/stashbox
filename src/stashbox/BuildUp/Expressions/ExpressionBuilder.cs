﻿using Stashbox.Entity;
using Stashbox.Entity.Resolution;
using Stashbox.Exceptions;
using Stashbox.Infrastructure;
using Stashbox.Infrastructure.ContainerExtension;
using Stashbox.Infrastructure.Registration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Stashbox.BuildUp.Expressions
{
    internal class ExpressionBuilder : IExpressionBuilder
    {
        private readonly IContainerContext containerContext;
        private readonly IContainerExtensionManager containerExtensionManager;

        public ExpressionBuilder(IContainerContext containerContext, IContainerExtensionManager containerExtensionManager)
        {
            this.containerContext = containerContext;
            this.containerExtensionManager = containerExtensionManager;
        }

        public Expression CreateFillExpression(IServiceRegistration serviceRegistration, Expression instance,
            ResolutionInfo resolutionInfo, Type serviceType)
        {
            var block = new List<Expression>();

            if (instance.Type != serviceType)
                instance = Expression.Convert(instance, serviceType);

            var variable = Expression.Variable(serviceType);
            var assign = Expression.Assign(variable, instance);

            block.Add(assign);

            if (serviceRegistration.MetaInformation.InjectionMembers.Length > 0)
                block.AddRange(this.FillMembersExpression(serviceRegistration, resolutionInfo, variable));

            if (serviceRegistration.MetaInformation.InjectionMethods.Length > 0 || this.containerExtensionManager.HasPostBuildExtensions)
                block.AddRange(this.CreatePostWorkExpressionIfAny(serviceRegistration, resolutionInfo, serviceType, variable));

            block.Add(variable); //return

            return Expression.Block(new[] { variable }, block);
        }

        public Expression CreateExpression(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo, Type serviceType)
        {
            var initExpression = this.CreateInitExpression(serviceRegistration, resolutionInfo);
            if (initExpression == null)
                return null;

            if (serviceRegistration.MetaInformation.InjectionMembers.Length > 0)
                initExpression = Expression.MemberInit((NewExpression)initExpression, this.GetMemberBindings(serviceRegistration, resolutionInfo));

            if (serviceRegistration.MetaInformation.InjectionMethods.Length > 0 || this.containerExtensionManager.HasPostBuildExtensions)
            {
                var variable = Expression.Variable(initExpression.Type);
                var assign = Expression.Assign(variable, initExpression);

                var expressions = this.CreatePostWorkExpressionIfAny(serviceRegistration, resolutionInfo, serviceType, variable);

                var block = new List<Expression>(expressions.Length + 1) { assign };
                block.AddRange(expressions);
                block.Add(variable);

                return Expression.Block(new[] { variable }, block);
            }

            return initExpression;
        }

        private Expression CreateInitExpression(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo)
        {
            if (serviceRegistration.RegistrationContext.SelectedConstructor != null)
            {
                if (serviceRegistration.RegistrationContext.ConstructorArguments != null)
                    return Expression.New(serviceRegistration.RegistrationContext.SelectedConstructor, serviceRegistration.RegistrationContext.ConstructorArguments.Select(Expression.Constant));

                var resolutionConstructor = this.CreateResolutionConstructor(serviceRegistration, resolutionInfo, serviceRegistration.RegistrationContext.SelectedConstructor);
                return Expression.New(resolutionConstructor.Constructor, resolutionConstructor.Parameters);
            }

            var rule = serviceRegistration.RegistrationContext.ConstructorSelectionRule ?? this.containerContext.ContainerConfigurator.ContainerConfiguration.ConstructorSelectionRule;
            var constructors = rule(serviceRegistration.MetaInformation.Constructors).ToArray();

            var constructor = this.SelectConstructor(serviceRegistration, resolutionInfo, constructors);
            return constructor == null ? null : Expression.New(constructor.Constructor, constructor.Parameters);
        }

        private ResolutionConstructor CreateResolutionConstructor(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo, ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            var paramLength = parameters.Length;
            var parameterExpressions = new Expression[paramLength];

            for (var i = 0; i < paramLength; i++)
            {
                var parameter = parameters[i];

                var expression = this.containerContext.ResolutionStrategy.BuildResolutionExpression(this.containerContext,
                    resolutionInfo, serviceRegistration.MetaInformation.GetTypeInformationForParameter(parameter),
                    serviceRegistration.RegistrationContext.InjectionParameters);

                parameterExpressions[i] = expression ?? throw new ResolutionFailedException(serviceRegistration.ImplementationType,
                    $"Constructor {constructor}, unresolvable parameter: ({parameter.ParameterType}){parameter.Name}");
            }

            return new ResolutionConstructor { Constructor = constructor, Parameters = parameterExpressions };
        }

        private ResolutionConstructor SelectConstructor(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo, ConstructorInformation[] constructors)
        {
            var length = constructors.Length;
            var checkedConstructors = new Dictionary<ConstructorInfo, TypeInformation>();
            for (var i = 0; i < length; i++)
            {
                var constructor = constructors[i];
                var paramLength = constructor.Parameters.Length;
                var parameterExpressions = new Expression[paramLength];

                var hasNullParameter = false;
                TypeInformation failedParameter = null;
                for (var j = 0; j < paramLength; j++)
                {
                    var parameter = constructor.Parameters[j];

                    var expression = this.containerContext.ResolutionStrategy.BuildResolutionExpression(this.containerContext,
                        resolutionInfo, parameter, serviceRegistration.RegistrationContext.InjectionParameters);

                    if (expression == null)
                    {
                        hasNullParameter = true;
                        failedParameter = parameter;
                        break;
                    }

                    parameterExpressions[j] = expression;
                }

                if (hasNullParameter)
                {
                    if (!resolutionInfo.NullResultAllowed)
                        checkedConstructors.Add(constructor.Constructor, failedParameter);

                    continue;
                }

                return new ResolutionConstructor { Constructor = constructor.Constructor, Parameters = parameterExpressions };
            }

            if (resolutionInfo.NullResultAllowed)
                return null;

            var stringBuilder = new StringBuilder();
            foreach (var checkedConstructor in checkedConstructors)
                stringBuilder.AppendLine($"Checked constructor {checkedConstructor.Key}, unresolvable parameter: ({checkedConstructor.Value.Type}){checkedConstructor.Value.ParameterName}");

            throw new ResolutionFailedException(serviceRegistration.ImplementationType, stringBuilder.ToString());
        }

        private Expression[] CreatePostWorkExpressionIfAny(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo, Type serviceType, Expression instance)
        {
            var length = serviceRegistration.MetaInformation.InjectionMethods.Length;
            var expressions = new Expression[this.containerExtensionManager.HasPostBuildExtensions ? length + 1 : length];

            if (length > 0)
                this.CreateMethodExpressions(serviceRegistration, resolutionInfo, instance, expressions);

            if (this.containerExtensionManager.HasPostBuildExtensions)
            {
                var call = Expression.Call(Expression.Constant(this.containerExtensionManager), Constants.BuildExtensionMethod, instance, Expression.Constant(this.containerContext),
                      Expression.Constant(resolutionInfo), Expression.Constant(serviceRegistration), Expression.Constant(serviceType));

                expressions[expressions.Length - 1] = Expression.Assign(instance, Expression.Convert(call, instance.Type));
            }

            return expressions;
        }

        private IEnumerable<Expression> FillMembersExpression(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo, Expression instance)
        {
            var length = serviceRegistration.MetaInformation.InjectionMembers.Length;

            var expressions = new List<Expression>();

            for (var i = 0; i < length; i++)
            {
                var member = serviceRegistration.MetaInformation.InjectionMembers[i];

                if (!serviceRegistration.CanInjectMember(member)) continue;

                var expression = this.containerContext.ResolutionStrategy
                    .BuildResolutionExpression(this.containerContext, resolutionInfo, member.TypeInformation, serviceRegistration.RegistrationContext.InjectionParameters);

                if (expression == null) continue;

                if (member.MemberInfo is PropertyInfo prop)
                {
                    var propExpression = Expression.Property(instance, prop);
                    expressions.Add(Expression.Assign(propExpression, expression));
                }
                else if (member.MemberInfo is FieldInfo field)
                {
                    var propExpression = Expression.Field(instance, field);
                    expressions.Add(Expression.Assign(propExpression, expression));
                }
            }

            return expressions;
        }

        private void CreateMethodExpressions(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo, Expression newExpression, Expression[] buffer)
        {
            var length = serviceRegistration.MetaInformation.InjectionMethods.Length;
            for (var i = 0; i < length; i++)
            {
                var info = serviceRegistration.MetaInformation.InjectionMethods[i];

                var paramLength = info.Parameters.Length;
                if (paramLength == 0)
                    buffer[i] = Expression.Call(newExpression, info.Method, Constants.EmptyExpressions);
                else
                {
                    var parameters = new Expression[paramLength];
                    for (var j = 0; j < paramLength; j++)
                        parameters[j] = this.containerContext.ResolutionStrategy.BuildResolutionExpression(this.containerContext, resolutionInfo,
                            info.Parameters[j], serviceRegistration.RegistrationContext.InjectionParameters);

                    buffer[i] = Expression.Call(newExpression, info.Method, parameters);
                }
            }
        }

        private IEnumerable<MemberBinding> GetMemberBindings(IServiceRegistration serviceRegistration, ResolutionInfo resolutionInfo)
        {
            var length = serviceRegistration.MetaInformation.InjectionMembers.Length;
            var members = new List<MemberBinding>();

            for (var i = 0; i < length; i++)
            {
                var info = serviceRegistration.MetaInformation.InjectionMembers[i];
                if (!serviceRegistration.CanInjectMember(info)) continue;

                var expression = this.containerContext.ResolutionStrategy
                    .BuildResolutionExpression(this.containerContext, resolutionInfo, info.TypeInformation, serviceRegistration.RegistrationContext.InjectionParameters);

                if (expression == null) continue;

                members.Add(Expression.Bind(info.MemberInfo, expression));
            }

            return members;
        }
    }
}
