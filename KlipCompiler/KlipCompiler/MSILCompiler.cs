using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace KlipCompiler
{

    interface ISystem
    {
        void PrintLine(string msg);
        void Print(string msg);
        string InputString();
        string ReadLine();
    }
    class MSILCompiler
    {
        TypeBuilder _baseType;
        AssemblyBuilder _assemblyBuilder;
        private MethodBuilder _currentMethod;

        public MSILCompiler(List<Stmt> list, string scriptname)
        {
            _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new System.Reflection.AssemblyName("Script"), AssemblyBuilderAccess.RunAndSave);
            var mdBuilder = _assemblyBuilder.DefineDynamicModule(scriptname, scriptname +".exe", false);
            _baseType = mdBuilder.DefineType("ScriptClass");

            CompileStmtList(list);
            _baseType.CreateType();
            _assemblyBuilder.Save(scriptname + ".exe");
        }


        private Stack<bool> _ifBlockHasElse = new Stack<bool>();
        private void CompileStmtList(List<Stmt> list)
        {
            int cnt = 0;

            foreach (Stmt s in list)
            {
                if (s is Func)
                {
                    CompileFunc((Func)s);
                }
                else if (s is IfBlock)
                {

                    if (list.Count > cnt + 1)
                    {
                        if (list[cnt + 1] is ElseBlock || list[cnt + 1] is ElseIfBlock)
                            _ifBlockHasElse.Push(true);
                    }
                    else
                    {
                        _ifBlockHasElse.Push(false);
                    }
                    CompileIf((IfBlock)s);
                }
                else if (s is ElseIfBlock)
                {
                    if (list.Count > cnt + 1)
                    {
                        if (list[cnt + 1] is ElseBlock || list[cnt + 1] is ElseIfBlock)
                            _ifBlockHasElse.Push(true);
                    }
                    else
                    {
                        _ifBlockHasElse.Push(false);
                    }
                    CompileElseIf((ElseIfBlock)s);
                }
                else if (s is ElseBlock)
                {
                    CompileElse((ElseBlock)s);
                }
                else if (s is EndIf)
                {

                    if (_ifBlockHasElse.Count > 0 && _ifBlockHasElse.Pop() == true)
                    {
                        var currentElseLabel = _currentMethod.GetILGenerator().DefineLabel();
                        _currentMethod.GetILGenerator().Emit(OpCodes.Br_S, currentElseLabel);
                        _labelElse.Push(currentElseLabel);
                    }

                    _currentMethod.GetILGenerator().Emit(OpCodes.Nop);
                    if (_labelEndIf.Count > 0)
                    {
                        var currentEndIfLabel = _labelEndIf.Pop();
                        _currentMethod.GetILGenerator().MarkLabel(currentEndIfLabel);
                    }
                    else if (_labelElse.Count > 0)
                    {
                        while (_labelElse.Count > 0)
                        {
                            var currentElseLabel = _labelElse.Pop();
                            _currentMethod.GetILGenerator().MarkLabel(currentElseLabel);
                        }
                    }

                    // currentEndIfLabel = new Label();
                    //Write("endif");
                }
                else if (s is RepeatBlock)
                {
                    CompileRepeat((RepeatBlock)s);
                }
                else if (s is Assign)
                {
                    CompileAssign((Assign)s);
                }
                else if (s is Call)
                {
                    CompileCall((Call)s);
                }
                else if (s is Return)
                {
                    var il = _currentMethod.GetILGenerator();

                    if (((Return)s).expr == null)
                    {
                        il.Emit(OpCodes.Ret);
                        //Write("ret");
                    }
                    else
                    {
                        //CompileExpr(((Return)s).expr);
                        // Write("ret");
                    }
                }
                cnt++;
            }
        }

        private string gets() { return "parttwo"; }


        static string assigntest, name;
        private static void ForTest()
        {
            int i;
            for (i = 0; i < 10; i++)
            {
                Console.WriteLine(i + "woo");
            }

        }
        private static void WhileTest()
        {
            int i = 0;
            while (i < 10)
            {
                Console.WriteLine(i + "woo");

            }

        }

        private void testelseif()
        {
            var x = "x";
            if (x == "x")
            {
                Console.WriteLine("equals x");
            }
            else if (x == "y")
            {
                Console.WriteLine("equals y");
            }
        }
        private static void testtt()
        {
            Console.Write("Whats");
            var x = "y";


            if ("z" == x)
            {
                Console.WriteLine("woo2 equals");
            }
            else
            {
                Console.WriteLine("woo2 !equals");
            }

        }

        private void CompileExpr(Expr data)
        {
            var il = _currentMethod.GetILGenerator();
            if (data is IntLiteral)
            {
                il.Emit(OpCodes.Ldind_I, ((IntLiteral)data).value);
            }
            else if (data is StringLiteral)
            {
                il.Emit(OpCodes.Ldstr, ((StringLiteral)data).value.Trim(new char[] { '"' }));
            }
            else if (data is Ident)
            {
                //Check if its a param

                var para = _currentMethodParams.FirstOrDefault(p => p.Name.Equals(((Ident)data).value));
                var field = _vars.FirstOrDefault(v => v.Name.Equals(((Ident)data).value));

                if (para != null)
                {
                    il.Emit(OpCodes.Ldarg, para.Position);
                }
                else if (field != null)
                {
                    il.Emit(OpCodes.Ldsfld, field);
                }
                else
                {
                    throw new Exception("Invalid Ident: " + ((Ident)data).value);
                }

            }
            else if (data is CallExpr)
            {
                Call c = new Call(((CallExpr)data).ident, ((CallExpr)data).args);
                CompileCall(c);
            }
            else if (data is MathExpr)
            {
                CompileExpr(((MathExpr)data).leftExpr);
                CompileExpr(((MathExpr)data).rightExpr);
                switch (((MathExpr)data).op)
                {
                    case Symbol.add:
                        if (((MathExpr)data).leftExpr is StringLiteral || ((MathExpr)data).rightExpr is StringLiteral)
                            il.EmitCall(OpCodes.Call, typeof(String).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }), new Type[] { typeof(string), typeof(string) });
                        else
                            il.Emit(OpCodes.Add);
                        break;
                    case Symbol.div:
                        il.Emit(OpCodes.Div);
                        break;
                    case Symbol.sub:
                        il.Emit(OpCodes.Sub);
                        break;
                    case Symbol.mul:
                        il.Emit(OpCodes.Mul);
                        break;
                    default:
                        throw new Exception("Unhandled opcode");
                }
            }
            else if (data is ParanExpr)
            {
                CompileExpr(((ParanExpr)data).value);
            }
            return;
        }
        internal Type[] GetArgTypes(List<Expr> args)
        {
            List<Type> types = new List<Type>();
            foreach (var arg in args)
            {
                if (arg is StringLiteral)
                    types.Add(typeof(string));
                else if (arg is IntLiteral)
                    types.Add(typeof(int));
                else if (arg is CallExpr)
                    types.Add(typeof(object));
            }
            return types.ToArray();
        }
        private void EmitMethodCall(MethodBuilder m, List<Expr> args)
        {
            var il = _currentMethod.GetILGenerator();

            //args.Reverse();
            foreach (var arg in args)
            {
                CompileExpr(arg);
            }
            var paras = GetArgTypes(args);

            //m.SetParameters(paras);
            //if(m.ReturnType == null)
            // {
            il.EmitCall(OpCodes.Call, m, paras);
            //}

        }


        private void CompileCall(Call s)
        {
            var il = _currentMethod.GetILGenerator();

            //replace with checking an interface/class
            switch (s.ident)
            {
                case "Print":
                case "Log":
                    CompileExpr(s.args[0]);
                    var methodw = typeof(Console).GetMethod("Write", new Type[] { typeof(string) });
                    il.EmitCall(OpCodes.Call, methodw, new Type[] { typeof(string) });
                    return;
                case "PrintLine":
                    CompileExpr(s.args[0]);
                    var method = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) });
                    il.EmitCall(OpCodes.Call, method, new Type[] { typeof(string) });
                    return;
                    break;
                case "InputString":
                    var methodInput = typeof(Console).GetMethod("ReadLine");
                    il.EmitCall(OpCodes.Call, methodInput, null);
                    return;
                case "ReadLine":
                    //il.Emit(OpCodes.Ldstr, CompileExprToString(s.args[0]));
                    var methodRead = typeof(Console).GetMethod("ReadLine");
                    //il.DeclareLocal(typeof(string));
                    il.EmitCall(OpCodes.Call, methodRead, null);
                    il.Emit(OpCodes.Pop);
                    return;
                    break;

                    //ReadInt etc unsupported for now

            }
            //Check if its a defined method
            MethodBuilder definedMethod = _methods.FirstOrDefault(m => m.Name.Equals(s.ident));
            if (definedMethod != null)
            {
                //handle args and shit and return val
            }
            else
            {
                definedMethod = _baseType.DefineMethod(s.ident, MethodAttributes.Public | MethodAttributes.Static);
                _methods.Add(definedMethod);


            }
            EmitMethodCall(definedMethod, s.args);
        }

        List<FieldInfo> _vars = new List<FieldInfo>();
        private void CompileAssign(Assign s)
        {
            FieldInfo fi = null;
            if (_vars.FirstOrDefault(v => v.Name.Equals(s.ident)) == null)
            {
                fi = _baseType.DefineField(s.ident, typeof(string), FieldAttributes.Private | FieldAttributes.Static);
                _vars.Add(fi);
            }
            else
                fi = _vars.FirstOrDefault(v => v.Name.Equals(s.ident));
            var il = _currentMethod.GetILGenerator();
            //il.Emit(OpCodes.Ldarg_0);
            CompileExpr(s.value);
            il.Emit(OpCodes.Stsfld, fi);
        }

        private void CompileRepeat(RepeatBlock s)
        {
            var il = _currentMethod.GetILGenerator();
            var repeatLabel = il.DefineLabel();
            il.MarkLabel(repeatLabel);
            CompileStmtList(s.statements);
            il.Emit(OpCodes.Br_S, repeatLabel);
        }



        private void CompileElse(ElseBlock s)
        {
            var il = _currentMethod.GetILGenerator();
            CompileStmtList(s.statements);
        }

        private void CompileElseIf(ElseIfBlock s)
        {
            var il = _currentMethod.GetILGenerator();
            CompileExpr(s.leftExpr);
            CompileExpr(s.rightExpr);
            var localBool = il.DeclareLocal(typeof(bool));
            var cmpMethod = typeof(String).GetMethod("op_Equality");

            il.Emit(OpCodes.Call, cmpMethod);
            il.Emit(OpCodes.Stloc, localBool.LocalIndex);
            il.Emit(OpCodes.Ldloc, localBool.LocalIndex);
            var currentEndIfLabel = il.DefineLabel();
            if (s.op == Symbol.doubleEqual)
            {
                il.Emit(OpCodes.Brfalse_S, currentEndIfLabel);
            }
            else if (s.op == Symbol.notEqual)
            {
                il.Emit(OpCodes.Brtrue_S, currentEndIfLabel);
            }
            _labelEndIf.Push(currentEndIfLabel);
            CompileStmtList(s.statements);
        }


        private void CompileIf(IfBlock s)
        {
            var il = _currentMethod.GetILGenerator();

            CompileExpr(s.leftExpr);
            CompileExpr(s.rightExpr);
            var localBool = il.DeclareLocal(typeof(bool));
            var cmpMethod = typeof(String).GetMethod("op_Equality");

            il.Emit(OpCodes.Call, cmpMethod);
            il.Emit(OpCodes.Stloc, localBool.LocalIndex);
            il.Emit(OpCodes.Ldloc, localBool.LocalIndex);
            var currentEndIfLabel = il.DefineLabel();
            if (s.op == Symbol.doubleEqual)
            {
                il.Emit(OpCodes.Brfalse_S, currentEndIfLabel);
            }
            else if (s.op == Symbol.notEqual)
            {
                il.Emit(OpCodes.Brtrue_S, currentEndIfLabel);
            }
            _labelEndIf.Push(currentEndIfLabel);
            CompileStmtList(s.statements);



        }

        List<MethodBuilder> _methods = new List<MethodBuilder>();


        private Stack<Label> _labelEndIf = new Stack<Label>();
        private Stack<Label> _labelElse = new Stack<Label>();
        private List<ParameterBuilder> _currentMethodParams = new List<ParameterBuilder>();

        private void CompileFunc(Func s)
        {
            //if (_currentMethod != null)
            //   _methods.Add(_currentMethod);
            if (_currentMethodParams.Count > 0)
            {
                _currentMethodParams.Clear();
            }

            var isDefinedMethod = _methods.FirstOrDefault(m => m.Name.Equals(s.ident));
            if (isDefinedMethod != null)
            {
                _currentMethod = isDefinedMethod;
            }
            else
            {
                _currentMethod = null;
                // if(s.ident.Equals("Main"))
                if (s.vars.Count == 0)
                    _currentMethod = _baseType.DefineMethod(s.ident, System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static);
                else
                {
                    var types = new List<Type>();
                    foreach (var a in s.vars)
                    {
                        types.Add(typeof(string));
                    }
                    _currentMethod = _baseType.DefineMethod(s.ident, System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static, typeof(void), types.ToArray());

                }
                // else
                //     _currentMethod = _baseType.DefineMethod(s.ident, System.Reflection.MethodAttributes.Public);

            }
            _methods.Add(_currentMethod);
            int i = 0;
            foreach (var a in s.vars)
            {
                var para = _currentMethod.DefineParameter(i++, ParameterAttributes.None, a);

                _currentMethodParams.Add(para);
            }


            if (s.ident.Equals("Main"))
            {
                _assemblyBuilder.SetEntryPoint(_currentMethod);
            }
            //TODO handle vars
            CompileStmtList(s.statements);

        }

        /// <summary>
        /// return msil as string
        /// </summary>
        /// <returns></returns>
        public string GetCode()
        {
            return "";
        }
    }
}
