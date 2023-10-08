using System.Collections.Generic;

namespace SimpleList.Services
{
    public class AccountService
    {
        private readonly Dictionary<string, OneDrive> _accounts;

        public OneDrive GetAccount(string name)
        {
            return _accounts[name];
        }

        public Dictionary<string, OneDrive> GetAccounts()
        {
            return _accounts;
        }

        public void AddAccount(string name, OneDrive drive)
        {
            _accounts.Add(name, drive);
        }

        public void RemoveAccount(string name)
        {
            _accounts.Remove(name);
        }
    }
}
