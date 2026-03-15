
namespace AdaptiveCodecContextEngine.Models;



public abstract record Error(string Code, string Details);


public record DatabaseError(string Code, string Details): Error(Code, Details)
{
    
}
