using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;

namespace MergeClasses {

    class Log {

        System.IO.StreamWriter file;

        public Log(String logfile) {
            this.file = new System.IO.StreamWriter(logfile);
        }

        ~Log() {
            try {
                this.file.Close();
            }
            catch {
            }
        }

        public void writeLine(String line) {
            this.file.WriteLine(line);
            this.file.Flush();
        }

    }


    class Program {

        // for the moment a global log object
        static Log globalLog;



        static void copyMethod(MethodDefinition dest, IMethodDefinition source, PeReader.DefaultHost host) {

            // only copy body if it is not a dummy (= empty)
            if (!(source.Body is Microsoft.Cci.Dummy)) {
                // TODO
                // langsames kopieren des Bodies, schnellerer weg möglich?
                var testIlGenerator = new ILGenerator(host, dest);
                foreach (var operation in source.Body.Operations) {
                    testIlGenerator.Emit(operation.OperationCode, operation.Value);
                }
                List<ILocalDefinition> variableListCopy = new List<ILocalDefinition>(source.Body.LocalVariables);
                List<ITypeDefinition> privateHelperTypesListCopy = new List<ITypeDefinition>(source.Body.PrivateHelperTypes);
                var newBody = new ILGeneratorMethodBody(testIlGenerator, source.Body.LocalsAreZeroed, source.Body.MaxStack, dest, variableListCopy, privateHelperTypesListCopy);
                dest.Body = newBody;
            }

            dest.CallingConvention = source.CallingConvention;
            if (source.IsGeneric)
                dest.GenericParameters = new List<IGenericMethodParameter>(source.GenericParameters);
            else
                dest.GenericParameters = null;
            if (source.ParameterCount > 0)
                dest.Parameters = new List<IParameterDefinition>(source.Parameters);
            else
                dest.Parameters = null;
            if (source.IsPlatformInvoke)
                dest.PlatformInvokeData = source.PlatformInvokeData;
            else
                dest.PlatformInvokeData = Dummy.PlatformInvokeInformation;
            dest.ReturnValueAttributes = new List<ICustomAttribute>(source.ReturnValueAttributes);
            if (source.ReturnValueIsModified)
                dest.ReturnValueCustomModifiers = new List<ICustomModifier>(source.ReturnValueCustomModifiers);
            else
                dest.ReturnValueCustomModifiers = new List<ICustomModifier>(0);
            if (source.ReturnValueIsMarshalledExplicitly)
                dest.ReturnValueMarshallingInformation = source.ReturnValueMarshallingInformation;
            else
                dest.ReturnValueMarshallingInformation = Dummy.MarshallingInformation;
            if (source.HasDeclarativeSecurity && IteratorHelper.EnumerableIsNotEmpty(source.SecurityAttributes))
                dest.SecurityAttributes = new List<ISecurityAttribute>(source.SecurityAttributes);
            else
                dest.SecurityAttributes = null;
            dest.Type = source.Type;
            dest.AcceptsExtraArguments = source.AcceptsExtraArguments;
            dest.HasDeclarativeSecurity = source.HasDeclarativeSecurity;
            dest.IsAbstract = source.IsAbstract;
            dest.IsAccessCheckedOnOverride = source.IsAccessCheckedOnOverride;
            dest.IsCil = source.IsCil;
            dest.IsExternal = source.IsExternal;
            dest.IsForwardReference = source.IsForwardReference;
            dest.IsHiddenBySignature = source.IsHiddenBySignature;
            dest.IsNativeCode = source.IsNativeCode;
            dest.IsNewSlot = source.IsNewSlot;
            dest.IsNeverInlined = source.IsNeverInlined;
            dest.IsAggressivelyInlined = source.IsAggressivelyInlined;
            dest.IsNeverOptimized = source.IsNeverOptimized;
            dest.IsPlatformInvoke = source.IsPlatformInvoke;
            dest.IsRuntimeImplemented = source.IsRuntimeImplemented;
            dest.IsRuntimeInternal = source.IsRuntimeInternal;
            dest.IsRuntimeSpecial = source.IsRuntimeSpecial;
            dest.IsSealed = source.IsSealed;
            dest.IsSpecialName = source.IsSpecialName;
            dest.IsStatic = source.IsStatic;
            dest.IsSynchronized = source.IsSynchronized;
            dest.IsUnmanaged = source.IsUnmanaged;
            if (dest.IsStatic)
                dest.IsVirtual = false;
            else
                dest.IsVirtual = source.IsVirtual;
            dest.PreserveSignature = source.PreserveSignature;
            dest.RequiresSecurityObject = source.RequiresSecurityObject;
            dest.ReturnValueIsByRef = source.ReturnValueIsByRef;
            dest.ReturnValueIsMarshalledExplicitly = source.ReturnValueIsMarshalledExplicitly;
            dest.ReturnValueName = source.ReturnValueName;
            dest.Name = source.Name;
            dest.Visibility = source.Visibility;
        }


        static void copyField(FieldDefinition dest, FieldDefinition source) {
            if (source.IsBitField)
                dest.BitLength = (uint)source.BitLength;
            else
                dest.BitLength = uint.MaxValue;
            dest.CompileTimeValue = source.CompileTimeValue;
            dest.IsCompileTimeConstant = source.IsCompileTimeConstant;
            if (source.IsModified)
                dest.CustomModifiers = new List<ICustomModifier>(source.CustomModifiers);
            else
                dest.CustomModifiers = null;
            if (source.IsMapped)
                dest.FieldMapping = source.FieldMapping;
            else
                dest.FieldMapping = Dummy.SectionBlock;
            if (source.IsMarshalledExplicitly)
                dest.MarshallingInformation = source.MarshallingInformation;
            else
                dest.MarshallingInformation = Dummy.MarshallingInformation;
            if (source.ContainingTypeDefinition.Layout == LayoutKind.Explicit)
                dest.Offset = source.Offset;
            else
                dest.Offset = 0;
            if (source.ContainingTypeDefinition.Layout == LayoutKind.Sequential)
                dest.SequenceNumber = source.SequenceNumber;
            else
                dest.SequenceNumber = 0;
            dest.Type = source.Type;
            dest.IsNotSerialized = source.IsNotSerialized;
            dest.IsReadOnly = source.IsReadOnly;
            dest.IsSpecialName = source.IsSpecialName;
            if (source.IsRuntimeSpecial) {
                //^ assume dest.IsSpecialName;
                dest.IsRuntimeSpecial = source.IsRuntimeSpecial;
            }
            else
                dest.IsRuntimeSpecial = false;
            dest.IsStatic = source.IsStatic;
            dest.Name = source.Name;
            dest.Visibility = source.Visibility;
        }







        static void mergeClassWithAncestor(NamespaceTypeDefinition mergeTargetClass, PeReader.DefaultHost host) {

            // get class from which was inherited
            ITypeDefinition ancestorClass = null;
            INamedEntity ancestorClassName = null;
            if (mergeTargetClass.BaseClasses.Count == 1) {

                // TODO
                // entferne wenn Methode umgeschrieben
                /*
                // ignore inheritance from System.Object
                if (mergeTargetClass.BaseClasses[0].ToString() != "System.Object") {
                    ancestorClass = (mergeTargetClass.BaseClasses[0] as NamespaceTypeDefinition);
                }
                // return if ancestor class equals System.Object
                else {
                    return;
                }
                */



                // check base class is of type NamespaceTypeDefinition
                if (mergeTargetClass.BaseClasses.ElementAt(0) as NamespaceTypeDefinition != null) {
                    ancestorClass = (mergeTargetClass.BaseClasses.ElementAt(0) as ITypeDefinition);
                    ancestorClassName = (mergeTargetClass.BaseClasses.ElementAt(0) as INamedEntity);
                }                
                // ignore inheritance from System.Object
                else if (mergeTargetClass.BaseClasses[0].ToString() == "System.Object") {                                       
                    return;                 
                }
                // check for needed values in base class and its resolved value (for example when base class of type NamespaceTypeReference)
                else if ((mergeTargetClass.BaseClasses.ElementAt(0).ResolvedType as ITypeDefinition != null)
                    && (mergeTargetClass.BaseClasses.ElementAt(0) as INamedEntity != null)) {
                    // TODO
                    // weiss nicht ob es Sinn macht externe Klassen wie z.B. system.ValueType
                    // in die aktuelle Klasse rein zu mergen => denn Trick mit methoden auf Virtual stellen
                    // damit JIT Compiler unsere Methoden aufruft klappt nicht in dem Fall (da wir die Methoden von System.ValueType nicht umschreiben koennen)
                    //ancestorClass = (mergeTargetClass.BaseClasses[0].ResolvedType as ITypeDefinition);
                    //ancestorClassName = (mergeTargetClass.BaseClasses[0].ResolvedType as INamedEntity);
                    return;
                }
                else {
                    throw new ArgumentException("Do not know how to handle base class.");
                }
            }
            else {
                throw new ArgumentException("Do not know how to handle multiple inheritance.");
            }


            // copy all attributes from the ancestor class to the current class
            if (ancestorClass.Fields != null) {
                globalLog.writeLine("Copying fileds");
                foreach (FieldDefinition field in ancestorClass.Fields) {
                    FieldDefinition copy = new FieldDefinition();
                    globalLog.writeLine("Copying field: " + field.Name.ToString());
                    copyField(copy, field);
                    copy.ContainingTypeDefinition = mergeTargetClass;

                    // set intern factory of the copied field to the one of the class
                    // (without it, the generated binary file will have strange results like use only the same field)
                    copy.InternFactory = mergeTargetClass.InternFactory;

                    mergeTargetClass.Fields.Add(copy);
                }
                globalLog.writeLine("");
            }

            // list of all constructors of the ancestor class
            List<MethodDefinition> copiedAncestorConstructors = new List<MethodDefinition>();            

            // copy methods from the ancestor class to the current class
            if (ancestorClass.Methods != null) {
                globalLog.writeLine("Copying methods");
                foreach (IMethodDefinition method in ancestorClass.Methods) {
                    globalLog.writeLine("Copying method: " + method.Name.ToString());

                    // check if the method is a constructor
                    if (method.IsConstructor) {
                        MethodDefinition copiedAncestorConstructor = new MethodDefinition();
                        copyMethod(copiedAncestorConstructor, method, host);

                        // TODO
                        // name muss noch geaendert werden
                        String tempName = "ctor_" + ancestorClassName.Name.ToString();
                        copiedAncestorConstructor.Name = host.NameTable.GetNameFor(tempName);

                        // constructor has the "SpecialName" (the name describes the functionality)
                        // and "RTSpecialName" (runtime should check name encoding) flag set 
                        // runtime will raise an exception if these flags are set for a copied constructor
                        // which is added as a normal function 
                        copiedAncestorConstructor.IsSpecialName = false;
                        copiedAncestorConstructor.IsRuntimeSpecial = false;




                        // TODO:
                        // vielleicht einfach von späterer Schleife machen lassen (sollte jetzt schon gehen, aber noch nicht getestet,
                        // deshalb erst ein mal drin gelassen)
                        // copy constructor and rewrite instructions to use attributes and functions
                        // inside the same class
                        var testIlGenerator = new ILGenerator(host, copiedAncestorConstructor);
                        foreach (var operation in copiedAncestorConstructor.Body.Operations) {
                            switch (operation.OperationCode) {

                                case OperationCode.Call:

                                    // HIER NOCH CHECKEN OB ANDERE FUNKTIONEN INNERHALB DES ANCESTORS AUFGERUFEN WERDEN
                                    // DIESE MUESSEN UMGELEITET WERDEN

                                    /*
                                    Microsoft.Cci.MutableCodeModel.MethodDefinition blah = null;
                                    if (operation.Value is Microsoft.Cci.MutableCodeModel.MethodDefinition) {
                                        blah = (Microsoft.Cci.MutableCodeModel.MethodDefinition)(operation.Value);
                                        NamespaceTypeDefinition test = (NamespaceTypeDefinition)blah.ContainingTypeDefinition;

                                        if (ancestorClass.Name.Equals(test.Name)) {
                                            System.Console.WriteLine("Ja");
                                        }


                                    }
                                    */

                                    testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                    break;

                                case OperationCode.Stfld:

                                    FieldDefinition attribute = (FieldDefinition)operation.Value;

                                    // search for the method attribute that is used in the copied constructor
                                    // and replace it with the method attribute from the current class
                                    FieldDefinition foundField = null;
                                    foreach (FieldDefinition field in mergeTargetClass.Fields) {
                                        if (attribute.Name.Equals(field.Name)
                                            && attribute.Type.Equals(field.Type)) {
                                            foundField = field;
                                            break;
                                        }
                                    }
                                    if (foundField == null) {
                                        System.Console.WriteLine("Attribute not found");
                                        return;
                                    }

                                    testIlGenerator.Emit(operation.OperationCode, foundField);

                                    break;

                                default:
                                    testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                    break;

                            }
                        }

                        // create new body
                        List<ILocalDefinition> variableListCopy = new List<ILocalDefinition>(method.Body.LocalVariables);
                        List<ITypeDefinition> privateHelperTypesListCopy = new List<ITypeDefinition>(method.Body.PrivateHelperTypes);
                        var newBody = new ILGeneratorMethodBody(testIlGenerator, method.Body.LocalsAreZeroed, method.Body.MaxStack, copiedAncestorConstructor, variableListCopy, privateHelperTypesListCopy);
                        copiedAncestorConstructor.Body = newBody;

                        // set intern factory of the copied constructor to the one of the class
                        // (without it, the generated binary file will have strange results like call always the same method)
                        copiedAncestorConstructor.InternFactory = mergeTargetClass.InternFactory;

                        // add copied constructor to the class
                        copiedAncestorConstructor.ContainingTypeDefinition = mergeTargetClass;
                        copiedAncestorConstructor.Visibility = TypeMemberVisibility.Private;
                        mergeTargetClass.Methods.Add(copiedAncestorConstructor);

                        // add copied ancestor constructor to the list of copied ancestor constructors
                        copiedAncestorConstructors.Add(copiedAncestorConstructor);

                        continue;
                    }

                    else {

                        // copy method
                        MethodDefinition copy = new MethodDefinition();
                        copyMethod(copy, method, host);
                        copy.ContainingTypeDefinition = mergeTargetClass;

                        // set intern factory of the copied method to the one of the class
                        // (without it, the generating of the binary file will have strange results)
                        copy.InternFactory = mergeTargetClass.InternFactory;

                        // add method to the class
                        if (mergeTargetClass.Methods == null) {
                            mergeTargetClass.Methods = new List<IMethodDefinition>();
                        }
                        mergeTargetClass.Methods.Add(copy);
                    }
                }
                globalLog.writeLine("");
            }


            // rewrite constructors of the class to call the copied constructor instead of
            // the constructor of the ancestor class
            // (rewriting methods is handled later)
            foreach (MethodDefinition method in mergeTargetClass.Methods) {

                // check if method is a constructor
                if (method.IsConstructor) {

                    var testIlGenerator = new ILGenerator(host, method);
                    foreach (var operation in method.Body.Operations) {

                        switch (operation.OperationCode) {

                            // only call instructions that call a constructor have to be redirected
                            case OperationCode.Call:

                                MethodDefinition calledMethod = (MethodDefinition)operation.Value;
                                if (calledMethod.IsConstructor) {

                                    // TODO hier treten noch Probleme auf, wenn System.Object ist nicht der oberste Ancestor
                                    // sondern z.B. System.ValueType
                                    // check if the called constructor is not the constructor of System.Object
                                    // and if the constructor that is called is not a constructor of THIS class
                                    NamespaceTypeDefinition calledMethodNamespace = (NamespaceTypeDefinition)calledMethod.ContainingTypeDefinition;
                                    if (calledMethodNamespace.ToString() != "System.Object"
                                        && !calledMethodNamespace.Name.Equals(mergeTargetClass.Name)) {

                                        // search for the copied constructor that has to be called
                                        // (it is searched by parameter types because they always are named the same ".ctor")
                                        MethodDefinition copiedConstructorToCall = null;
                                        foreach (MethodDefinition copiedAncestorConstructor in copiedAncestorConstructors) {

                                            // check if the count of the parameters of the called constructor and the copied constructor are the same
                                            if (copiedAncestorConstructor.ParameterCount == calledMethod.ParameterCount) {

                                                // check if the parameters have the same type in the same order
                                                bool found = true;
                                                for (int i = 0; i < calledMethod.ParameterCount; i++) {
                                                    if (calledMethod.Parameters[i].Type != copiedAncestorConstructor.Parameters[i].Type) {
                                                        found = false;
                                                        break;
                                                    }
                                                }
                                                if (found) {
                                                    copiedConstructorToCall = copiedAncestorConstructor;
                                                    break;
                                                }
                                            }
                                        }
                                        if (copiedConstructorToCall == null) {
                                            throw new ArgumentException("No copied constructor with the same parameter types in the same order.");
                                        }

                                        // call copied constructor instead of constructor of the ancestor
                                        testIlGenerator.Emit(operation.OperationCode, copiedConstructorToCall);

                                    }
                                    // just emit if the called constructor is the constructor of System.Object
                                    else {
                                        testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                    }
                                }

                                // just emit operation if no constructor is called
                                // (rewriting methods is handled later)
                                else {
                                    testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                }

                                break;

                            // just emit operation if none of the above cases match
                            default:
                                testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                break;
                        }
                    }

                    // create new body
                    List<ILocalDefinition> variableListCopy = new List<ILocalDefinition>(method.Body.LocalVariables);
                    List<ITypeDefinition> privateHelperTypesListCopy = new List<ITypeDefinition>(method.Body.PrivateHelperTypes);
                    var newBody = new ILGeneratorMethodBody(testIlGenerator, method.Body.LocalsAreZeroed, method.Body.MaxStack, method, variableListCopy, privateHelperTypesListCopy);
                    method.Body = newBody;
                }
            }


            // rewrite all methods to use local attributes/methods instead of the ones of the ancestor
            foreach (MethodDefinition method in mergeTargetClass.Methods) {

                var testIlGenerator = new ILGenerator(host, method);
                foreach (var operation in method.Body.Operations) {
                    switch (operation.OperationCode) {

                        case OperationCode.Stfld:
                        case OperationCode.Ldfld:

                            // get namespace of attribute
                            FieldDefinition attribute = (FieldDefinition)operation.Value;
                            INamedTypeDefinition attributeNamespace = (INamedTypeDefinition)attribute.ContainingTypeDefinition;

                            // check if namespace of attribute equals the namespace of THIS class
                            // => just emit operation
                            if (mergeTargetClass.Name.Equals(attributeNamespace.Name)) {
                                testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                continue;
                            }

                            // try the namespace of all ancestors of this class to see if it was copied
                            else {

                                // flag that indicates if the operation was emitted during the search loop
                                bool operationEmitted = false;

                                // search through all ancestors to find the namespace of the used attribute
                                NamespaceTypeDefinition tempAncestorClass = mergeTargetClass;
                                while (true) {

                                    // get class from which was inherited
                                    if (tempAncestorClass.BaseClasses.Count == 1) {
                                        // ignore inheritance from System.Object
                                        if (tempAncestorClass.BaseClasses[0].ToString() != "System.Object") {
                                            tempAncestorClass = (tempAncestorClass.BaseClasses[0] as NamespaceTypeDefinition);
                                        }
                                        // return if ancestor class equals System.Object
                                        else {
                                            break;
                                        }
                                    }
                                    else {
                                        throw new ArgumentException("Do not know how to handle multiple inheritance.");
                                    }

                                    // check namespace of used attribute is equal to namespace of ancestor
                                    // => change target of operation to THIS class                                        
                                    if (tempAncestorClass.Name.Equals(attributeNamespace.Name)) {

                                        // search for the method attribute that is used in the copied method
                                        // and replace it with the method attribute from the current class
                                        FieldDefinition foundField = null;
                                        foreach (FieldDefinition field in mergeTargetClass.Fields) {
                                            if (attribute.Name.Equals(field.Name)
                                                && attribute.Type.Equals(field.Type)) {
                                                foundField = field;
                                                break;
                                            }
                                        }
                                        if (foundField == null) {
                                            throw new ArgumentException("Attribute not found.");
                                        }

                                        // emit operation and set flag that it was emitted
                                        operationEmitted = true;
                                        testIlGenerator.Emit(operation.OperationCode, foundField);
                                        break;
                                    }
                                }

                                // if operation was not emitted yet => emit it
                                if (operationEmitted == false) {
                                    testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                }
                            }
                            break;

                        case OperationCode.Call:

                            // just emit if value is of type method reference
                            if (operation.Value is Microsoft.Cci.MutableCodeModel.MethodReference) {
                                testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                continue;
                            }

                            // get namespace of called method
                            MethodDefinition calledMethod = (MethodDefinition)operation.Value;
                            NamespaceTypeDefinition calledMethodNamespace = (NamespaceTypeDefinition)calledMethod.ContainingTypeDefinition;

                            // check if namespace of called method equals the namespace of THIS class
                            // => just emit operation
                            if (mergeTargetClass.Name.Equals(calledMethodNamespace.Name)) {
                                testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                continue;
                            }

                            // try the namespace of all ancestors of this class to see if it was copied
                            else {

                                // flag that indicates if the operation was emitted during the search loop
                                bool operationEmitted = false;

                                // search through all ancestors to find the namespace of the called method
                                NamespaceTypeDefinition tempAncestorClass = mergeTargetClass;
                                while (true) {

                                    // get class from which was inherited
                                    if (tempAncestorClass.BaseClasses.Count == 1) {
                                        // ignore inheritance from System.Object
                                        if (tempAncestorClass.BaseClasses[0].ToString() != "System.Object") {
                                            tempAncestorClass = (tempAncestorClass.BaseClasses[0] as NamespaceTypeDefinition);
                                        }
                                        // return if ancestor class equals System.Object
                                        else {
                                            break;
                                        }
                                    }
                                    else {
                                        throw new ArgumentException("Do not know how to handle multiple inheritance.");
                                    }

                                    // check namespace of called method is equal to namespace of ancestor, if the same
                                    // => find copied method in THIS class
                                    // => change target of operation to method in THIS class    
                                    if (tempAncestorClass.Name.Equals(calledMethodNamespace.Name)) {

                                        // search through all methods in THISS class and search for the copied one
                                        MethodDefinition foundMethod = null;
                                        foreach (MethodDefinition newMethod in mergeTargetClass.Methods) {

                                            // skip constructors (can not be called from the ancestor)
                                            if (newMethod.IsConstructor) {
                                                continue;
                                            }

                                            if (newMethod.ParameterCount == calledMethod.ParameterCount) {

                                                // check if the parameters have the same type in the same order
                                                bool parameterCorrect = true;
                                                for (int i = 0; i < calledMethod.ParameterCount; i++) {
                                                    if (calledMethod.Parameters[i].Type != newMethod.Parameters[i].Type) {
                                                        parameterCorrect = false;
                                                        break;
                                                    }
                                                }

                                                // if the parameters are correct => check the name
                                                if (parameterCorrect) {
                                                    if (calledMethod.Name.Equals(newMethod.Name)) {

                                                        // if name is the same => found method to call
                                                        foundMethod = newMethod;
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        // check if the method we want to call was found
                                        if (foundMethod == null) {
                                            throw new ArgumentException("Did not find a method to call.");
                                        }

                                        // emit operation and set flag that it was emitted
                                        operationEmitted = true;
                                        testIlGenerator.Emit(operation.OperationCode, foundMethod);
                                        break;

                                    }
                                }

                                // if operation was not emitted yet => emit it
                                if (operationEmitted == false) {
                                    testIlGenerator.Emit(operation.OperationCode, operation.Value);
                                }
                            }
                            break;

                        // just emit operation if none of the above cases match
                        default:
                            testIlGenerator.Emit(operation.OperationCode, operation.Value);
                            break;

                    }
                }

                // create new body
                List<ILocalDefinition> variableListCopy = new List<ILocalDefinition>(method.Body.LocalVariables);
                List<ITypeDefinition> privateHelperTypesListCopy = new List<ITypeDefinition>(method.Body.PrivateHelperTypes);
                var newBody = new ILGeneratorMethodBody(testIlGenerator, method.Body.LocalsAreZeroed, method.Body.MaxStack, method, variableListCopy, privateHelperTypesListCopy);
                method.Body = newBody;
            }
        }



        static void mergeClassWithAllAncestors(NamespaceTypeDefinition mergeTargetClass, PeReader.DefaultHost host) {

            List<NamespaceTypeDefinition> ancestorClasses = new List<NamespaceTypeDefinition>();
            ancestorClasses.Add(mergeTargetClass);

            NamespaceTypeDefinition currentClass = mergeTargetClass;
            NamespaceTypeDefinition ancestorClass = null;


            while (true) {

                // get class from which was inherited
                if (currentClass.BaseClasses.Count() == 1) {                    
                    // only add base classes of type NamespaceTypeDefinition
                    if (currentClass.BaseClasses.ElementAt(0) as NamespaceTypeDefinition != null) {
                        ancestorClass = (currentClass.BaseClasses.ElementAt(0) as NamespaceTypeDefinition);
                    }
                    // ignore ancestor that are not of type NamespaceTypeDefinition
                    else {
                        break;
                    }
                }
                else {
                    throw new ArgumentException("Do not know how to handle multiple inheritance.");
                }

                // add ancestor class to list of ancestor classes
                globalLog.writeLine("Found ancestor: " + ancestorClass.ToString());
                ancestorClasses.Add(ancestorClass);

                currentClass = ancestorClass;
            }

            // mrge class with ancenstor (start with the "highest" ancestor)
            for (int i = (ancestorClasses.Count - 1); i >= 0; i--) {
                globalLog.writeLine("Merge class \"" + ancestorClasses[i].ToString() + "\" with ancestor");
                mergeClassWithAncestor(ancestorClasses[i], host);
                globalLog.writeLine("");
            }


        }





        static void Main(string[] args) {

            //String inputFile = @"c:\Users\typ\Documents\Visual Studio 2013\Projects\vererbung\vererbung\bin\Debug\vererbung.exe";
            String inputFile = @"e:\dile_v0_2_12_x86\Dile.exe";

            

            globalLog = new Log(@"e:\merge_logfile.txt");

            System.Console.WriteLine(inputFile);

            /*
            if (args == null || args.Length == 0)
            {
                Console.WriteLine("Usage: ILMutator <assembly> [<outputPath>]");
                return;
            }
            */




            using (var host = new PeReader.DefaultHost()) {
                //IModule/*?*/ module = host.LoadUnitFrom(args[0]) as IModule;
                IModule/*?*/ module = host.LoadUnitFrom(inputFile) as IModule;







                if (module == null || module == Dummy.Module || module == Dummy.Assembly) {
                    //Console.WriteLine(args[0] + " is not a PE file containing a CLR module or assembly.");
                    Console.WriteLine(inputFile + " is not a PE file containing a CLR module or assembly.");
                    return;
                }

                module = new MetadataDeepCopier(host).Copy(module);

                if (module as Assembly == null) {
                    globalLog.writeLine("File does not have CIL assembly");
                    return;
                }


                PdbReader/*?*/ pdbReader = null;
                string pdbFile = Path.ChangeExtension(module.Location, "pdb");
                if (File.Exists(pdbFile)) {
                    using (var pdbStream = File.OpenRead(pdbFile)) {
                        pdbReader = new PdbReader(pdbStream, host);
                    }
                }
                else {
                    globalLog.writeLine("Could not load the PDB file for '" + module.Name.Value + "' . Proceeding anyway.");
                }

                using (pdbReader) {

                    Microsoft.Cci.ILGenerator.LocalScopeProvider localScopeProvider = null;
                    if (pdbReader != null) {
                        localScopeProvider = new ILGenerator.LocalScopeProvider(pdbReader);
                    }











                    // AB HIER DER INTERESSANTE PART ZUM MERGEN, DAVOR NUR INIT CRAP

                    // search for class in namespace
                    NamespaceTypeDefinition foundClass = null;
                    foreach (var asdf in module.GetAllTypes()) {
                        //if (asdf.ToString() == "vererbung.Erber") {
                        if (asdf.ToString() == "Dile.Disassemble.Assembly") {                            
                            foundClass = (asdf as NamespaceTypeDefinition);
                            break;
                        }
                    }
                    if (foundClass == null) {
                        globalLog.writeLine("Class not found!");
                        return;
                    }

                    // merge class
                    globalLog.writeLine("Merge class \"" + foundClass.ToString() + "\" with all ancestors");
                    mergeClassWithAllAncestors(foundClass, host);






                    string newName;
                    if (args.Length == 2) {
                        newName = args[1];
                        //newName = @"e:\dile_v0_2_12_x86\dile_classes.exe";
                        newName = @"e:\test.exe";
                    }
                    else {
                        var loc = module.Location;
                        var path = Path.GetDirectoryName(loc) ?? "";
                        var fileName = Path.GetFileNameWithoutExtension(loc);
                        var ext = Path.GetExtension(loc);
                        newName = Path.Combine(path, fileName + "1" + ext);
                        //newName = @"e:\dile_v0_2_12_x86\dile_classes.exe";
                        newName = @"e:\test.exe";
                    }

                    using (var peStream = File.Create(newName)) {
                        using (var pdbWriter = new PdbWriter(Path.ChangeExtension(newName, ".pdb"), pdbReader)) {
                            PeWriter.WritePeToStream(module, host, peStream, pdbReader, localScopeProvider, pdbWriter);
                        }
                    }
                }
            }
        }
    }
}
