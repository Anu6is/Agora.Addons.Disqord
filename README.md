<h1 align="center">Auction Bot</h1>
<p align="center">Powering seamless auction/sales/trades/giveaways in your Discord server.</p>
<div align="center">
  <a href="https://discord.gg/WmCpC8G">
    <img src="https://discordapp.com/api/guilds/392774790945046538/widget.png?style=banner2" alt="Discord Banner 2"/>
  </a>
</div>
<h1> </h1>

### What is [Auction Bot](https://auction-bot.github.io/docs/)?
Auction Bot is your all-in-one solution for hosting auctions, giveaways and more within Discord.

<div align="center">
  <img alt="Logo" src="https://github.com/Anu6is/Agora.Addons.Disqord/assets/4596077/1941bda7-516e-4a79-8063-b768680f182c" style="width: 30%"/>
</div>

## Customizable
Designed to allow Discord servers to host different types of listings while giving complete control over the process. 
Select the listings that should be available and decide where they should be posted. 
Ensure a secure and organized experience by specifying which Discord roles can create listings or submit offers.

## Flexible Economy
Auction Bot offers a user-friendly market system for Discord servers, making it easy to manage and trade items. 
It also includes a basic economy system that tracks user balances effortlessly. 
Moreover, Auction Bot provides the flexibility to integrate with other Discord bots such as **UnbelievaBoat** or **Raid Helper**, enhancing the overall economy experience for server members.

## Open Source
Feel free to contribute features, bug fixes or translations to help improve Auction Bot.  
Community feature suggestions are availabe in the Discord server.

## Contributing
Thank you for wanting to make a contribution!

To start, you'd need to create a [Discord Bot Application](https://discord.com/developers/applications) if you don't already have one. This is required so you can run your own instance of **Auction Bot** and test your changes.
### Creating a Bot Account 
<details> 

- Make sure you are logged on to the [Discord website](https://discord.com/).  <br/>
- Navigate to the [application page](https://discord.com/developers/applications)  <br/>
- Click on the “**New Application**” button.  <br/>
- Give the application a name and click “**Create**”.  <br/>
- Navigate to the “**Bot**” tab to configure it.  <br/>
- Copy the **token** using the “Copy” button and store it later.  <br/><br/>

And that’s it. You now have a bot account that you can invite to a server.  

</details>

### Inviting Your Bot 
<details>
Continuing from where you left off above

- Go to the “**OAuth2**” tab. <br/>
- Scroll to the **OAuth2 URL Generator**. <br/>
- Tick the “bot” checkbox under “scopes”. <br/>
- Tick the permissions required for the bot under “Bot Permissions”. <br/>
  * Read Messages <br/>
  * Send Messages <br/>
  * Send Messages in Threads <br/>
  * Create Public Threads <br/>
  * Manage Threads <br/>
  * Embed Links <br/>
  * Use External Emoji <br/>
  * Use Application Commands <br/>
- Now the resulting URL can be used to add your bot to a server. Copy and paste the URL into your browser, choose a server to invite the bot to, and click “Authorize”.  <br/>
</details>

### Building the Source Code
<details>
  
The first thing you'd need to do is [Fork](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/fork-a-repo) this repository.

Once you have the code downloaded 
- Set the **Launcher** project as the startup project <br/>
- Open the `appsettings.json` file in the **Launcher** project <br/>
   * Paste your bot **token** where you see **SUPER_SECRET_DISCORD_APPLICATION_TOKEN** <br/>
- Execute/Run the project <br/>

Your bot should now appear Online in your server. All existing **Auction Bot** features should be available to your new bot.  
</details>

### Making Changes
<details> 
  
If you are editing existing features you can simply make your changes, recompile the code and restart the application.  

If you are adding a new features, you can follow the `Extension.TransactionFees` project.  
If your project includes database chagnes, add your assembly name in the **Assemblies** list in the `appsettings.json` file.  
If you added new commands in your project, include the assembly name in the **Addons** list.  

Once you've tested your changes, feel free to submit a PR including details on how users are expected to interact with the changes and any configuration/settings updates that would be required.  
</details>
