using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using System.Collections.Generic;
using System.Linq;

namespace Weavers
{
    public class AgentWeaver : BaseModuleWeaver
    {

        private MethodDefinition wrapperMethod;
        private MethodReference completeTaskRef;
        private MethodReference getIsRemotingMethodRef;
        private MethodReference getRpcAgentMethodRef;
        private MethodReference getActorMethodRef;
        private TypeDefinition wrapperType;
        private MethodReference objConsRef;
        private MethodReference new_chain_ref;
        private MethodReference enqueue_Func;
        private MethodDefinition privateMethodDef;
        private TypeDefinition agentInterfType;
        public override void Execute()
        {
            WriteMessage($"AgentWeaver Start：{ModuleDefinition.Assembly.Name}", MessageImportance.High);
            //import
            var baseCompTypeDef = FindTypeDefinition("Geek.Server.BaseComponent");
            if (baseCompTypeDef == null)
            {
                WriteError("获取Geek.Server.BaseComponent类型失败");
                return;
            }
            var compGetActorMethodDef = baseCompTypeDef.Methods.FirstOrDefault(md => md.Name == "get_Actor");
            if (compGetActorMethodDef == null)
            {
                WriteError("获取Geek.Server.BaseComponent.Actor属性失败");
                return;
            }

            var taskDef = FindTypeDefinition("System.Threading.Tasks.Task");
            var completeTaskDef = taskDef.Methods.FirstOrDefault(md => md.Name == "get_CompletedTask");
            completeTaskRef = ModuleDefinition.ImportReference(completeTaskDef);

            var compGetActorMethodRef = ModuleDefinition.ImportReference(compGetActorMethodDef);
            var actorType = compGetActorMethodDef.ReturnType.Resolve().BaseType.Resolve();
            var actorSendAsync_Action_Def = actorType.Methods.FirstOrDefault(md => md.FullName.Contains("Enqueue(System.Action,"));
            var actorSendAsync_Func_Def = actorType.Methods.FirstOrDefault(md => md.FullName.Contains("Enqueue(System.Func`1<T>,"));
            var actorSendAsync_Task_Action_Def = actorType.Methods.FirstOrDefault(md => md.FullName.Contains("Enqueue(System.Func`1<System.Threading.Tasks.Task>,"));
            var actorSendAsync_Task_Func_Def = actorType.Methods.FirstOrDefault(md => md.FullName.Contains("Enqueue(System.Func`1<System.Threading.Tasks.Task`1<T>>,"));
            var enqueue_Func_def = actorType.Methods.FirstOrDefault(md => md.FullName.Contains("IsNeedEnqueue"));
            var new_chain_def = actorType.Methods.FirstOrDefault(md => md.FullName.Contains("NewChainId"));

            var actorSendAsync_Func = ModuleDefinition.ImportReference(actorSendAsync_Func_Def);
            var actorSendAsync_Action = ModuleDefinition.ImportReference(actorSendAsync_Action_Def);
            var actorSendAsync_Task_Action = ModuleDefinition.ImportReference(actorSendAsync_Task_Action_Def);
            var actorSendAsync_Task_Func = ModuleDefinition.ImportReference(actorSendAsync_Task_Func_Def);
            enqueue_Func = ModuleDefinition.ImportReference(enqueue_Func_def);
            new_chain_ref = ModuleDefinition.ImportReference(new_chain_def);

            var func_def = actorSendAsync_Task_Action.Parameters[0].ParameterType.Resolve().Methods.FirstOrDefault(md => md.Name == ".ctor");

            var objDef = FindTypeDefinition("System.Object");
            var objConsDef = objDef.Methods.FirstOrDefault(md => md.Name == ".ctor");
            var objRef = ModuleDefinition.ImportReference(objDef);
            objConsRef = ModuleDefinition.ImportReference(objConsDef);

            //遍历dll中的所有类型
            var adds = new List<TypeDefinition>(ModuleDefinition.GetTypes());
            foreach (var typeDef in adds)
            {
                //排除抽象类
                if (typeDef.IsAbstract)
                    continue;

                bool isAgentComp = false;
                var interfaceArr = typeDef.GetAllInterfaces();
                foreach (var face in interfaceArr)
                {
                    if (face.FullName == "Geek.Server.IComponentAgent")
                    {
                        isAgentComp = true;
                        break;
                    }
                }
                //排除非IComponentAgent
                if (!isAgentComp)
                    continue;

                //检查Agent是否二次继承
                if (!typeDef.BaseType.Resolve().FullName.StartsWith("Geek.Server.StateComponentAgent")
                    && !typeDef.BaseType.Resolve().FullName.StartsWith("Geek.Server.FuncComponentAgent")
                    && !typeDef.BaseType.Resolve().FullName.StartsWith("Geek.Server.QueryComponentAgent"))
                {
                    WriteError($"{typeDef.FullName} Agent必须直接继承于StateComponentAgent或FuncComponentAgent，不允许二次继承", typeDef.Methods.Count > 0 ? typeDef.Methods[0] : null);
                    continue;
                }

                //创建wrapper
                wrapperType = new TypeDefinition("Wrapper.Agent", typeDef.Name + "Wrapper", typeDef.Attributes, typeDef);
                //创建interface
                agentInterfType = new TypeDefinition("Wrapper.Agent", "I" + typeDef.Name, typeDef.Attributes);
                agentInterfType.IsInterface = true;
                wrapperType.Interfaces.Add(new InterfaceImplementation(agentInterfType));

                ModuleDefinition.Types.Add(wrapperType);
                ModuleDefinition.Types.Add(agentInterfType);

                //处理所有Public函数
                int methodIndex = 0;
                foreach (var mthDef in typeDef.Methods)
                {
                    //静态函数
                    if (mthDef.IsStatic)
                        continue;
                    bool needWrap = mthDef.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName.StartsWith("Geek.Server.MethodOption")) != null;
                    if (!mthDef.IsPublic && !needWrap)
                        continue;

                    if (needWrap)
                    {
                        if (!mthDef.IsVirtual)
                            WriteError($"MethodOption标注的函数必须是Virtual函数 {typeDef.FullName}.{mthDef.Name}", mthDef);
                        mthDef.IsPublic = true;
                    }

                    //这两个函数由框架底层调度（Active, Deactive）
                    if (mthDef.Name == "Active" || mthDef.Name == "Deactive")
                        continue;

                    // 非构造的public函数返回值需要是Task形式
                    if (!mthDef.IsConstructor && !mthDef.ReturnType.FullName.StartsWith("System.Threading.Tasks.Task"))
                    {
                        WriteError($"{mthDef.DeclaringType.FullName}.{mthDef.Name} 返回值非Task，应修改为Task形式", mthDef);
                        continue;
                    }

                    //ref out检测
                    foreach (var para in mthDef.Parameters)
                    {
                        if (para.IsOut || para.ParameterType.IsByReference)
                        {
                            WriteError($"{typeDef.FullName}.{mthDef.Name} Agent public函数不允许使用ref或者out参数", mthDef);
                            break;
                        }
                    }

                    //注解设置
                    bool isNotAwait = mthDef.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "NotAwait") != null;
                    bool isLongTime = mthDef.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "LongTimeTask") != null;
                    bool isThreadSafe = mthDef.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "ThreadSafe") != null;
                    //线程安全则直接执行，且没有标记notawait直接跳过不用处理
                    if (isThreadSafe && !isNotAwait)
                        continue;

                    //构造函数
                    if (mthDef.IsConstructor)
                    {
                        var cons = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, TypeSystem.VoidReference);
                        var consIns = cons.Body.Instructions;
                        consIns.Append(
                            Instruction.Create(OpCodes.Ldarg_0),
                            Instruction.Create(OpCodes.Call, mthDef),
                            Instruction.Create(OpCodes.Nop),
                            Instruction.Create(OpCodes.Ret));
                        wrapperType.Methods.Add(cons);
                        continue;
                    }

                    //将agent的public函数都标记为虚函数
                    mthDef.IsVirtual = true;
                    mthDef.IsNewSlot = true; //virtual
                    mthDef.IsFinal = false;  //interface

                    bool isGenericMethod = mthDef.GenericParameters.Count > 0;

                    //为接口生成函数申明
                    var wrapperInterfMethod = new MethodDefinition(mthDef.Name, mthDef.Attributes, mthDef.ReturnType);
                    //为wrapper类生成函数申明
                    wrapperMethod = new MethodDefinition(mthDef.Name, mthDef.Attributes ^ MethodAttributes.NewSlot, mthDef.ReturnType);
                    for (int p = 0; p < mthDef.Parameters.Count; ++p)
                    {
                        wrapperInterfMethod.Parameters.Add(mthDef.Parameters[p]);
                        wrapperMethod.Parameters.Add(mthDef.Parameters[p]);
                    }
                    for (int i = 0; i < mthDef.GenericParameters.Count; i++)
                    {
                        var orgTypeParam = mthDef.GenericParameters[i];
                        var targetTypeParam = new GenericParameter(orgTypeParam.Name, wrapperMethod) { Attributes = orgTypeParam.Attributes }; //includes non-type constraints
                        foreach (var c in orgTypeParam.Constraints)//where约束
                            targetTypeParam.Constraints.Add(c);
                        wrapperMethod.GenericParameters.Add(targetTypeParam);
                        wrapperInterfMethod.GenericParameters.Add(targetTypeParam);
                    }
                    agentInterfType.Methods.Add(wrapperInterfMethod);
                    wrapperType.Methods.Add(wrapperMethod);




                    //注入生成函数体
                    wrapperMethod.Body = new MethodBody(wrapperMethod);

                    //找到对应的compAgent.Actor函数Ref
                    TypeDefinition stComp = FindBaseCompType(typeDef);
                    var getActorMethodDef = stComp.Methods.FirstOrDefault(m => m.Name == "get_Actor");
                    getActorMethodDef.IsPublic = true;
                    getActorMethodRef = ModuleDefinition.ImportReference(getActorMethodDef).MakeGeneric(((GenericInstanceType)(typeDef.BaseType)).GenericArguments[0]);

                    //找到对应的compAgent.IsRemoting函数Ref
                    var getIsRemotingMethodDef = stComp.Methods.FirstOrDefault(m => m.Name == "get_IsRemoting");
                    getIsRemotingMethodDef.IsPublic = true;
                    getIsRemotingMethodRef = ModuleDefinition.ImportReference(getIsRemotingMethodDef).MakeGeneric(((GenericInstanceType)(typeDef.BaseType)).GenericArguments[0]);

                    //找到对应的compAgent.IsRemoting函数Ref
                    var getRpcAgentMethodDef = stComp.Methods.FirstOrDefault(m => m.Name == "GetRpcAgent");
                    getIsRemotingMethodDef.IsPublic = true;
                    getRpcAgentMethodRef = ModuleDefinition.ImportReference(getRpcAgentMethodDef).MakeGeneric(agentInterfType);

                    MethodReference sendRef;
                    if (mthDef.ReturnType.FullName == "System.Threading.Tasks.Task")
                    {
                        // Func<Task>
                        sendRef = actorSendAsync_Task_Action;
                    }
                    else
                    {
                        if (isNotAwait)
                        {
                            WriteError($"{typeDef.FullName}.{mthDef.Name} NotAwait返回值只能是Task", mthDef);
                            continue;
                        }
                        // Func<Task<T>>
                        sendRef = ModuleDefinition.ImportReference(actorSendAsync_Task_Func_Def.MakeGenericMethod(((GenericInstanceType)(mthDef.ReturnType)).GenericArguments[0]));
                    }

                    //返回值
                    var returnFuncRef = ModuleDefinition.ImportReference(func_def).MakeGeneric(mthDef.ReturnType);

                    var instructions = wrapperMethod.Body.Instructions;
                    //首先判读是否为远程组件(远程组件不用判断直接入队)
                    //IsRemoting(mthDef, isNotAwait, isGenericMethod, instructions, methodIndex, returnFuncRef, isLongTime, sendRef);

                    //WriteError("instructions:" + instructions.Count);
                    //线程安全且不等待_则返回Task.completeTask
                    if (isThreadSafe && isNotAwait)
                    {
                        On_ThreadSafe_NotAwait(wrapperMethod.Parameters.Count > 0, isGenericMethod, instructions, mthDef, completeTaskRef);
                        continue;
                    }
                    //代码

                    //生成入队IL代码
                    //var enqueNop = Instruction.Create(OpCodes.Nop);
                    var enqueNop = GenEnqueueILCode(mthDef, isNotAwait, isGenericMethod, instructions);

                    //判断是否有参数,有参数需要生成内部类
                    if (wrapperMethod.Parameters.Count > 0)
                    {
                        //创建私有方法,调用Agent函数
                        CreateInnerMethod(mthDef, isGenericMethod, methodIndex, getIsRemotingMethodRef, getRpcAgentMethodRef, agentInterfType);
                        wrapperType.Methods.Add(privateMethodDef);
                        //创建内部类
                        CreateInnerClass(mthDef, methodIndex, privateMethodDef);
                        //调用内部类
                        CallInnerClass(mthDef, innerWrapper, isGenericMethod, instructions, returnFuncRef, isLongTime, sendRef, isNotAwait, enqueNop);
                    }
                    else 
                    {
                        //没有参数
                        CallWithoutParam(mthDef, instructions, returnFuncRef, isLongTime, sendRef, isNotAwait, enqueNop);
                    }
                    wrapperMethod.Body.InitLocals = true;
                    wrapperMethod.Body.Variables.Add(new VariableDefinition(mthDef.ReturnType));
                }
            }
            WriteMessage($"AgentWeaver End：{ModuleDefinition.Assembly.Name}", MessageImportance.High);
        }

        private void CallWithoutParam(MethodDefinition mthDef, Collection<Instruction> instructions,
            MethodReference returnFuncRef, bool isLongTime, MethodReference sendRef, bool isNotAwait, Instruction enqueNop)
        {
            var ldloc0 = Instruction.Create(OpCodes.Ldloc_1);
            instructions.Append(
                enqueNop,
                Instruction.Create(OpCodes.Ldarg_0),//this
                Instruction.Create(OpCodes.Call, getActorMethodRef),//.Actor
                Instruction.Create(OpCodes.Ldarg_0),//this
                Instruction.Create(OpCodes.Ldftn, mthDef),//base.func
                Instruction.Create(OpCodes.Newobj, returnFuncRef),//lamda
                Instruction.Create(OpCodes.Ldloc_0),//chainId
                Instruction.Create(OpCodes.Ldc_I4, isLongTime ? 30000 : 12000),//timeout
                Instruction.Create(OpCodes.Callvirt, sendRef));//actor.SendAsync

            if (isNotAwait)
            {
                instructions.Append(
                    Instruction.Create(OpCodes.Pop),
                    Instruction.Create(OpCodes.Call, completeTaskRef),
                    Instruction.Create(OpCodes.Stloc_1),
                    Instruction.Create(OpCodes.Br_S, ldloc0),
                    ldloc0,
                    Instruction.Create(OpCodes.Ret));
            }
            else
            {
                instructions.Append(
                    Instruction.Create(OpCodes.Stloc_1),
                    Instruction.Create(OpCodes.Br_S, ldloc0),
                    ldloc0,
                    Instruction.Create(OpCodes.Ret));
            }
        }

        private Instruction GenEnqueueILCode(MethodDefinition mthDef, bool isNotAwait, bool isGenericMethod, Collection<Instruction> instructions)
        {
            //局部变量定义，isNeedEnqueue或者NewChainID
            wrapperMethod.Body.Variables.Add(new VariableDefinition(TypeSystem.Int64Reference));

            if (isNotAwait)
            {
                var enqueNop = Instruction.Create(OpCodes.Nop);
                //不等，获取新的chainId入队执行
                //long chainId = base.actor.NewChainId();
                instructions.Append(
                    Instruction.Create(OpCodes.Nop),
                    Instruction.Create(OpCodes.Ldarg_0),//this
                    Instruction.Create(OpCodes.Call, getActorMethodRef),//.Actor
                    Instruction.Create(OpCodes.Call, new_chain_ref),//.NewChainId
                    Instruction.Create(OpCodes.Stloc_0));//id存到寄存器
                return enqueNop;
            }
            else
            {
                //判断是否为远程组件
                ////if(IsRemoting)
                instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                instructions.Add(Instruction.Create(OpCodes.Call, getIsRemotingMethodRef));
                var notRomting = Instruction.Create(OpCodes.Nop);
                instructions.Add(Instruction.Create(OpCodes.Brtrue_S, notRomting));
                //if body

                //判断当前是否需要入队执行
                //long id = base.actor.IsNeedEnqueue();
                //if(callChainId < 0) return base.action(....);
                //instructions.Add(Instruction.Create(OpCodes.Nop));
                instructions.Add(Instruction.Create(OpCodes.Ldarg_0));//this
                instructions.Add(Instruction.Create(OpCodes.Call, getActorMethodRef));//.Actor
                instructions.Add(Instruction.Create(OpCodes.Call, enqueue_Func));//.IsNeedEnqueue
                instructions.Add(Instruction.Create(OpCodes.Stloc_0));//id存到寄存器
                instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0)); //0
                instructions.Add(Instruction.Create(OpCodes.Ldloc_0));//取出id
                instructions.Add(Instruction.Create(OpCodes.Cgt)); //比较

                var enqueNop = Instruction.Create(OpCodes.Nop);
                instructions.Add(Instruction.Create(OpCodes.Brfalse_S, enqueNop));

                //参数
                instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                for (int i = 0; i < mthDef.Parameters.Count; i++)
                {
                    ParameterDefinition param = mthDef.Parameters[i];
                    var code = GetCodeByFieldIndex(i + 1);
                    if (code == OpCodes.Ldarg_S)
                        instructions.Add(Instruction.Create(code, param));
                    else
                        instructions.Add(Instruction.Create(code));
                }

                if (isGenericMethod)
                {
                    var result = new GenericInstanceMethod(mthDef);
                    foreach (var item in mthDef.GenericParameters)
                        result.GenericArguments.Add(item);
                    instructions.Add(Instruction.Create(OpCodes.Call, result));
                }
                else
                {
                    instructions.Add(Instruction.Create(OpCodes.Call, mthDef));
                }
                instructions.Add(Instruction.Create(OpCodes.Ret));


                var romting = Instruction.Create(OpCodes.Nop);
                instructions.Add(notRomting);
                instructions.Add(romting);
                wrapperMethod.Body.OptimizeMacros();
                //end if (IsRemoting)

                return enqueNop;
            }
        }


        private void CallInnerClass(MethodDefinition mthDef, TypeDefinition innerWrapper, bool isGenericMethod, Collection<Instruction> instructions,
            MethodReference returnFuncRef, bool isLongTime, MethodReference sendRef, bool isNotAwait, Instruction enqueNop)
        {
            ////局部变量定义 内部类
            var genericInnner = new GenericInstanceType(innerWrapper);
            foreach (var item in wrapperMethod.GenericParameters)
                genericInnner.GenericArguments.Add(item);
            if (isGenericMethod)
                wrapperMethod.Body.Variables.Add(new VariableDefinition(genericInnner));
            else
                wrapperMethod.Body.Variables.Add(new VariableDefinition(ModuleDefinition.ImportReference(innerWrapper)));


            //构造内部类,并对参数赋值
            instructions.Append(
                enqueNop,
                Instruction.Create(OpCodes.Newobj, isGenericMethod ? new MethodReference(innerConsDef.Name, innerConsDef.ReturnType, genericInnner)
                {
                    ExplicitThis = innerConsDef.ExplicitThis,
                    HasThis = innerConsDef.HasThis
                } : ModuleDefinition.ImportReference(innerConsDef)),//内部类构造函数
                Instruction.Create(OpCodes.Stloc_1));

            //参数赋值
            for (int i = 0; i < innerWrapper.Fields.Count; i++)
            {
                var code = GetCodeByFieldIndex(i);
                var fieldDef = innerWrapper.Fields[i];
                instructions.Add(Instruction.Create(OpCodes.Ldloc_1));
                if (code == OpCodes.Ldarg_S)
                    instructions.Add(Instruction.Create(code, mthDef.Parameters[i - 1]));
                else
                    instructions.Add(Instruction.Create(code));
                instructions.Add(Instruction.Create(OpCodes.Stfld, isGenericMethod ? new FieldReference(fieldDef.Name, fieldDef.FieldType, genericInnner) : fieldDef));
            }

            //入队调用base.action(...)
            var ldloc1 = Instruction.Create(OpCodes.Ldloc_2);
            instructions.Append(
                Instruction.Create(OpCodes.Nop),
                Instruction.Create(OpCodes.Ldarg_0),//this
                Instruction.Create(OpCodes.Call, getActorMethodRef),//.Actor
                Instruction.Create(OpCodes.Ldloc_1),//第1个局部变量，内部类实例
                Instruction.Create(OpCodes.Ldftn, isGenericMethod ? new MethodReference(innerMethod.Name, innerMethod.ReturnType, genericInnner)
                {
                    ExplicitThis = innerMethod.ExplicitThis,
                    HasThis = innerMethod.HasThis
                } : innerMethod),// 内部类 方法
                Instruction.Create(OpCodes.Newobj, returnFuncRef),//Func的构造方法
                Instruction.Create(OpCodes.Ldloc_0), //chainId
                Instruction.Create(OpCodes.Ldc_I4, isLongTime ? 30000 : 12000),//timeout
                Instruction.Create(OpCodes.Callvirt, sendRef));//sendAsync

            if (isNotAwait)
            {
                var ldloc0 = Instruction.Create(OpCodes.Ldloc_1);
                instructions.Append(
                    Instruction.Create(OpCodes.Pop),
                    Instruction.Create(OpCodes.Call, completeTaskRef),
                    Instruction.Create(OpCodes.Stloc_1),
                    Instruction.Create(OpCodes.Br_S, ldloc0),
                    ldloc0,
                    Instruction.Create(OpCodes.Ret));
            }
            else
            {
                instructions.Append(
                    Instruction.Create(OpCodes.Stloc_2),
                    Instruction.Create(OpCodes.Br_S, ldloc1),
                    ldloc1,
                    Instruction.Create(OpCodes.Ret));
            }
        }

        private TypeDefinition innerWrapper;
        private MethodDefinition innerConsDef;
        private MethodDefinition innerMethod;
        /// <summary>
        /// 创建内部类并赋值
        /// </summary>
        private void CreateInnerClass(MethodDefinition mthDef, int methodIndex, MethodDefinition privateMethodDef)
        {
            //是否为泛型函数
            bool isGenericMethod = mthDef.GenericParameters.Count > 0;

            //创建内部类
            innerWrapper = new TypeDefinition("Wrapper.Agent", $"<>Gen_c_{methodIndex++}_{mthDef.Name}", TypeAttributes.NestedPrivate | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed, TypeSystem.ObjectReference);
            wrapperType.NestedTypes.Add(innerWrapper);
            innerConsDef = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, ModuleDefinition.TypeSystem.Void);
            var ilProcessor = innerConsDef.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Call, objConsRef);
            ilProcessor.Emit(OpCodes.Nop);
            ilProcessor.Emit(OpCodes.Ret);
            innerWrapper.Methods.Add(innerConsDef);

            //添加泛型参数
            if (isGenericMethod)
            {
                foreach (var item in mthDef.GenericParameters)
                {
                    var gp = new GenericParameter(item.Name, innerWrapper);
                    foreach (var c in item.Constraints)
                        gp.Constraints.Add(c);
                    innerWrapper.GenericParameters.Add(gp);
                }
            }
           

            //变量存储参数
            innerWrapper.Fields.Add(new FieldDefinition($"outer", FieldAttributes.Public, wrapperType));
            foreach (ParameterDefinition pd in mthDef.Parameters)
                innerWrapper.Fields.Add(new FieldDefinition(pd.Name, FieldAttributes.Public, pd.ParameterType));


            //函数调用wrapper私有函数
            var innerDec = new GenericInstanceType(innerWrapper);
            foreach (var item1 in innerWrapper.GenericParameters)
                innerDec.GenericArguments.Add(item1);

            innerMethod = new MethodDefinition($"{mthDef.Name}", MethodAttributes.HideBySig | MethodAttributes.Assembly, mthDef.ReturnType);
            innerWrapper.Methods.Add(innerMethod);
            //var innerMethodIns = innerMethod.Body.Instructions;

            //传递MethodOption
            foreach (var att in mthDef.CustomAttributes)
            {
                var attName = att.AttributeType.FullName;
                if (attName.StartsWith("Geek.Server.MethodOption"))
                    innerMethod.CustomAttributes.Add(att);
            }

            var mthILProcessor = innerMethod.Body.GetILProcessor();
            //处理形参
            foreach (var item in innerWrapper.Fields)
            {
                mthILProcessor.Emit(OpCodes.Ldarg_0);
                mthILProcessor.Emit(OpCodes.Ldfld, isGenericMethod ? new FieldReference(item.Name, item.FieldType, innerDec) : item);
            }

            //处理所有泛型参数
            if (isGenericMethod)
            {
                var genericPrivateMethodRef = new GenericInstanceMethod(privateMethodDef);
                foreach (var item in innerWrapper.GenericParameters)
                    genericPrivateMethodRef.GenericArguments.Add(item);
                mthILProcessor.Emit(OpCodes.Call, genericPrivateMethodRef);
            }
            else
            {
                mthILProcessor.Emit(OpCodes.Call, privateMethodDef);
            }
            mthILProcessor.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// 创建内部类的外部调用函数
        /// </summary>
        private void CreateInnerMethod(MethodDefinition mthDef, bool isGenericMethod, int methodIndex,
            MethodReference getIsRemotingMethodRef,
            MethodReference getRpcAgentMethodRef,
            TypeDefinition agentInterface)
        {
            //创建私有方法,调用Agent函数
            privateMethodDef = new MethodDefinition($"<>Gen_m_{methodIndex}_{mthDef.Name}", MethodAttributes.Private | MethodAttributes.HideBySig, mthDef.ReturnType);
            //处理参数
            for (int i = 0; i < mthDef.Parameters.Count; i++)
            {
                ParameterDefinition param = mthDef.Parameters[i];
                privateMethodDef.Parameters.Add(param);
            }
            foreach (var item in mthDef.GenericParameters)
            {
                var gp = new GenericParameter(item.Name, privateMethodDef);
                foreach (var c in item.Constraints)//where约束
                    gp.Constraints.Add(c);
                privateMethodDef.GenericParameters.Add(gp);
            }


            /*********************IsRemoting*****************************/
            var ilProcessor = privateMethodDef.Body.GetILProcessor();
            //判断是否为远程组件
            //if(IsRemoting)
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Call, getIsRemotingMethodRef);
            var notRemotingBranch = ilProcessor.Create(OpCodes.Nop); //创建分支
            ilProcessor.Emit(OpCodes.Brfalse, notRemotingBranch);
            //if body

            //var foo = GetRpcAgent<Foo>();
            var agentInterf = new VariableDefinition(agentInterface);
            privateMethodDef.Body.Variables.Add(agentInterf);
            ilProcessor.Emit(OpCodes.Ldarg_0);
            var gi_GetRpcAgent = new GenericInstanceMethod(getRpcAgentMethodRef);
            gi_GetRpcAgent.GenericArguments.Add(agentInterface);
            ilProcessor.Emit(OpCodes.Call, gi_GetRpcAgent);
            ilProcessor.Emit(OpCodes.Stloc, agentInterf);

            //return foo.Test(a, list);
            ilProcessor.Emit(OpCodes.Ldloc, agentInterf);
            for (int i = 0; i < mthDef.Parameters.Count; i++)
            {
                ParameterDefinition param = mthDef.Parameters[i];
                var code = GetCodeByFieldIndex(i + 1);
                if (code == OpCodes.Ldarg_S)
                    ilProcessor.Emit(OpCodes.Ldarg_S, param);
                else
                    ilProcessor.Emit(code);
            }
            ilProcessor.Emit(OpCodes.Callvirt, privateMethodDef);
            ilProcessor.Emit(OpCodes.Ret);
            var lbl_elseEnd_20 = ilProcessor.Create(OpCodes.Nop);
            ilProcessor.Append(notRemotingBranch);
            ilProcessor.Append(lbl_elseEnd_20);
            ilProcessor.Body.OptimizeMacros();
            // end if (IsRemoting)
            /*********************IsRemoting*****************************/

            ilProcessor.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < mthDef.Parameters.Count; i++)
            {
                ParameterDefinition param = mthDef.Parameters[i];
                var code = GetCodeByFieldIndex(i + 1);
                if (code == OpCodes.Ldarg_S)
                    ilProcessor.Emit(code, param);
                else
                    ilProcessor.Emit(code);
            }

            if (isGenericMethod)
            {
                var result = new GenericInstanceMethod(mthDef);
                foreach (var item in mthDef.GenericParameters)
                    result.GenericArguments.Add(item);
                ilProcessor.Emit(OpCodes.Call, result);
            }
            else
            {
                ilProcessor.Emit(OpCodes.Call, mthDef);
            }

            ilProcessor.Emit(OpCodes.Ret);
        }



        private void On_ThreadSafe_NotAwait(bool hasParam, bool isGenericMethod, Collection<Instruction> instructions, 
            MethodDefinition mthDef, MethodReference completeTaskRef)
        {
            if (hasParam)
            {
                instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                for (int i = 0; i < mthDef.Parameters.Count; i++)
                {
                    ParameterDefinition param = mthDef.Parameters[i];
                    var code = GetCodeByFieldIndex(i + 1);
                    if (code == OpCodes.Ldarg_S)
                        instructions.Add(Instruction.Create(code, param));
                    else
                        instructions.Add(Instruction.Create(code));
                }

                if (isGenericMethod)
                {
                    var result = new GenericInstanceMethod(mthDef);
                    foreach (var item in mthDef.GenericParameters)
                        result.GenericArguments.Add(item);
                    instructions.Add(Instruction.Create(OpCodes.Call, result));
                }
                else
                {
                    instructions.Add(Instruction.Create(OpCodes.Call, mthDef));
                }

                instructions.Append(
                    Instruction.Create(OpCodes.Pop),
                    Instruction.Create(OpCodes.Call, completeTaskRef),
                    Instruction.Create(OpCodes.Ret));
            }
            else
            {
                var ldloc0 = Instruction.Create(OpCodes.Ldloc_1);
                instructions.Append(
                    Instruction.Create(OpCodes.Nop),
                    Instruction.Create(OpCodes.Ldarg_0),//this
                    Instruction.Create(OpCodes.Call, mthDef),//base.func
                    Instruction.Create(OpCodes.Pop),
                    Instruction.Create(OpCodes.Call, completeTaskRef),
                    Instruction.Create(OpCodes.Ret));
            }
        }

        private static TypeDefinition FindBaseCompType(TypeDefinition typeDef)
        {
            var stComp = typeDef.BaseType.Resolve();
            while (!stComp.FullName.StartsWith("Geek.Server.BaseComponentAgent")
                && !stComp.FullName.StartsWith("Geek.Server.QueryComponentAgent"))
                stComp = stComp.BaseType.Resolve();
            return stComp;
        }

        private static OpCode GetCodeByFieldIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return OpCodes.Ldarg_0;
                case 1:
                    return OpCodes.Ldarg_1;
                case 2:
                    return OpCodes.Ldarg_2;
                case 3:
                    return OpCodes.Ldarg_3;
                default:
                    return OpCodes.Ldarg_S;
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "System";
            yield return "mscorlib";
            yield return "GeekServer.Core";
            yield return "GeekServer.Hotfix";
            yield return "System.Collections";
        }
    }
}