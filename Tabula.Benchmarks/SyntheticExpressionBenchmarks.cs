using BenchmarkDotNet.Attributes;
using System;
using System.Linq.Expressions;
using System.Reflection.Emit;

namespace Tabula.Benchmarks;

[MemoryDiagnoser]
public class ExpressionAndIlGenerationBenchmarks
{
    private readonly ParameterExpression _x = Expression.Parameter(typeof(double), "x");
    private readonly ParameterExpression _y = Expression.Parameter(typeof(double), "y");

    [Benchmark(Baseline = true)]
    public Func<double, double, double> ExpressionTreeGeneration()
    {
        var body = Expression.Add(_x, Expression.Multiply(_y, Expression.Constant(2.0)));
        return Expression.Lambda<Func<double, double, double>>(body, _x, _y).Compile();
    }

    [Benchmark]
    public Func<double, double, double> IlGeneration()
    {
        var method = new DynamicMethod("SdfSynthetic", typeof(double), new[] { typeof(double), typeof(double) }, typeof(ExpressionAndIlGenerationBenchmarks).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldc_R8, 2.0);
        il.Emit(OpCodes.Mul);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ret);
        return (Func<double, double, double>)method.CreateDelegate(typeof(Func<double, double, double>));
    }
}

[MemoryDiagnoser]
public class ExpressionAndIlExecutionBenchmarks
{
    private const int _iterations = 100_000;
    private Func<double, double, double> _expression;
    private Func<double, double, double> _ilFunction;

    [GlobalSetup]
    public void Setup()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var y = Expression.Parameter(typeof(double), "y");
        _expression = Expression.Lambda<Func<double, double, double>>(
            Expression.Add(x, Expression.Multiply(y, Expression.Constant(2.0))), x, y).Compile();

        var method = new DynamicMethod("SdfSynthetic", typeof(double), new[] { typeof(double), typeof(double) }, typeof(ExpressionAndIlExecutionBenchmarks).Module, true);
        var generator = method.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Ldc_R8, 2.0);
        generator.Emit(OpCodes.Mul);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Ret);
        _ilFunction = (Func<double, double, double>)method.CreateDelegate(typeof(Func<double, double, double>));
    }

    [Benchmark(Baseline = true)]
    public double ExpressionTreeInvocation()
    {
        double sum = 0;
        for (int i = 0; i < _iterations; i++)
            sum += _expression(i, 3);
        return sum;
    }

    [Benchmark]
    public double IlDelegateInvocation()
    {
        double sum = 0;
        for (int i = 0; i < _iterations; i++)
            sum += _ilFunction(i, 3);
        return sum;
    }
}

[MemoryDiagnoser]
public class PartialAdapterGenerationBenchmarks
{
    private Func<double, double, double> _expression;
    private Func<double, double, double> _ilFunction;
    private Func<PartialState, Func<double, double>> _expressionPartialFactory;
    private DynamicMethod _ilPartialMethod;

    [GlobalSetup]
    public void Setup()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var y = Expression.Parameter(typeof(double), "y");
        _expression = Expression.Lambda<Func<double, double, double>>(
            Expression.Add(x, Expression.Multiply(y, Expression.Constant(2.0))), x, y).Compile();

        var method = new DynamicMethod("SdfSynthetic", typeof(double), new[] { typeof(double), typeof(double) }, typeof(PartialAdapterGenerationBenchmarks).Module, true);
        var generator = method.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Ldc_R8, 2.0);
        generator.Emit(OpCodes.Mul);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Ret);
        _ilFunction = (Func<double, double, double>)method.CreateDelegate(typeof(Func<double, double, double>));

        var state = Expression.Parameter(typeof(PartialState), "state");
        var arg = Expression.Parameter(typeof(double), "arg");
        var functionProperty = Expression.Property(state, nameof(PartialState.Function));
        var firstProperty = Expression.Property(state, nameof(PartialState.First));
        var invoke = Expression.Invoke(functionProperty, firstProperty, arg);
        _expressionPartialFactory = Expression.Lambda<Func<PartialState, Func<double, double>>>(
            Expression.Lambda<Func<double, double>>(invoke, arg), state).Compile();

        _ilPartialMethod = new DynamicMethod("SdfPartialInvoke", typeof(double), new[] { typeof(PartialState), typeof(double) }, typeof(PartialAdapterGenerationBenchmarks).Module, true);
        var bodyIl = _ilPartialMethod.GetILGenerator();
        bodyIl.Emit(OpCodes.Ldarg_0);
        bodyIl.Emit(OpCodes.Callvirt, typeof(PartialState).GetProperty(nameof(PartialState.Function)).GetMethod);
        bodyIl.Emit(OpCodes.Ldarg_0);
        bodyIl.Emit(OpCodes.Callvirt, typeof(PartialState).GetProperty(nameof(PartialState.First)).GetMethod);
        bodyIl.Emit(OpCodes.Ldarg_1);
        bodyIl.Emit(OpCodes.Callvirt, typeof(Func<double, double, double>).GetMethod("Invoke"));
        bodyIl.Emit(OpCodes.Ret);
    }

    [Benchmark(Baseline = true)]
    public Func<double, double> ExpressionTreeAdapterGeneration() => _expressionPartialFactory(new PartialState(_expression, 4));

    [Benchmark]
    public Func<double, double> IlAdapterGeneration() =>
        (Func<double, double>)_ilPartialMethod.CreateDelegate(typeof(Func<double, double>), new PartialState(_ilFunction, 4));
}

[MemoryDiagnoser]
public class PartialAdapterExecutionBenchmarks
{
    private const int _iterations = 100_000;
    private Func<double, double> _expressionPartial;
    private Func<double, double> _ilPartial;

    [GlobalSetup]
    public void Setup()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var y = Expression.Parameter(typeof(double), "y");
        var expression = Expression.Lambda<Func<double, double, double>>(
            Expression.Add(x, Expression.Multiply(y, Expression.Constant(2.0))), x, y).Compile();

        var method = new DynamicMethod("SdfSynthetic", typeof(double), new[] { typeof(double), typeof(double) }, typeof(PartialAdapterExecutionBenchmarks).Module, true);
        var generator = method.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Ldc_R8, 2.0);
        generator.Emit(OpCodes.Mul);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Ret);
        var ilFunction = (Func<double, double, double>)method.CreateDelegate(typeof(Func<double, double, double>));

        var state = Expression.Parameter(typeof(PartialState), "state");
        var arg = Expression.Parameter(typeof(double), "arg");
        var invoke = Expression.Invoke(Expression.Property(state, nameof(PartialState.Function)),
            Expression.Property(state, nameof(PartialState.First)), arg);
        var expressionFactory = Expression.Lambda<Func<PartialState, Func<double, double>>>(
            Expression.Lambda<Func<double, double>>(invoke, arg), state).Compile();
        _expressionPartial = expressionFactory(new PartialState(expression, 4));

        var body = new DynamicMethod("SdfPartialInvoke", typeof(double), new[] { typeof(PartialState), typeof(double) }, typeof(PartialAdapterExecutionBenchmarks).Module, true);
        var bodyIl = body.GetILGenerator();
        bodyIl.Emit(OpCodes.Ldarg_0);
        bodyIl.Emit(OpCodes.Callvirt, typeof(PartialState).GetProperty(nameof(PartialState.Function)).GetMethod);
        bodyIl.Emit(OpCodes.Ldarg_0);
        bodyIl.Emit(OpCodes.Callvirt, typeof(PartialState).GetProperty(nameof(PartialState.First)).GetMethod);
        bodyIl.Emit(OpCodes.Ldarg_1);
        bodyIl.Emit(OpCodes.Callvirt, typeof(Func<double, double, double>).GetMethod("Invoke"));
        bodyIl.Emit(OpCodes.Ret);
        _ilPartial = (Func<double, double>)body.CreateDelegate(typeof(Func<double, double>), new PartialState(ilFunction, 4));
    }

    [Benchmark(Baseline = true)]
    public double ExpressionTreeAdapterInvocation()
    {
        double sum = 0;
        for (int i = 0; i < _iterations; i++)
            sum += _expressionPartial(i);
        return sum;
    }

    [Benchmark]
    public double IlAdapterInvocation()
    {
        double sum = 0;
        for (int i = 0; i < _iterations; i++)
            sum += _ilPartial(i);
        return sum;
    }
}

public sealed class PartialState
{
    public Func<double, double, double> Function { get; }
    public double First { get; }

    public PartialState(Func<double, double, double> function, double first)
    {
        Function = function;
        First = first;
    }
}
