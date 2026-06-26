namespace Pharmatechnik.Nav.Language.CodeGen; 

sealed class CodeGeneratorContext {

    public CodeGeneratorContext(CodeGenerator generator) {
        Generator = generator;
    }

    public CodeGenerator Generator       { get; }
    public string        ProductVersion  => MyAssembly.ProductVersion;
    public bool          NullableContext => Generator.Options.NullableContext;
}