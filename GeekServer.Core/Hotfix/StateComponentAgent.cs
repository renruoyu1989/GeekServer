


namespace Geek.Server
{
    public abstract class StateComponentAgent<TComp, TState> : BaseComponentAgent<TComp> where TComp : BaseComponent, IState  where TState : DBState
    {
        public TState State
        {
            get
            {
                if (IsRemoting)
                {
                    //TODO
                    return default;
                }
                else
                {
                    return (TState)((IState)Owner).State;
                }
            }
        }
    }
}