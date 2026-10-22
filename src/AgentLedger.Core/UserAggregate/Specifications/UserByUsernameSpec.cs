namespace AgentLedger.Core.UserAggregate.Specifications;

public sealed class UserByUsernameSpec : SingleResultSpecification<User>
{
  public UserByUsernameSpec(Username username)
  {
    Query.Where(user => user.Username == username);
  }
}
