


We need to replace these 2 scripts(manage_gameobject.py and ManageGameObject.cs) manually. Because they are modified versions of https://github.com/CoderGamester/mcp-unity.git package.
So when you clone it from a different repo (i.e., Efe-Oral), the modified overriden scripts won't be used but the default scripts from the above package will be used. 
This is not we want because I extended the functionality of the above package. In order to get the extended functionality we have to change the content of the default downloaded package scripts.



1) Find ManageGameObject.cs script under Packages>Unity MCP Bridge>Editor>Tools>ManageGameObject
and replace its entire content with ManageGameObject_OVERRIDE under Assets>_Scripts>Override_Scripts>ManageGameObject_OVERRIDE



2) Find manage_gameobject.py script under C:\Users\<USERNAME>\AppData\Local\Programs\UnityMCP\UnityMcpServer\src\tools\manage_gameobject
and replace its entire content with manage_gameobject_OVERRIDE under Unity project's Assets>_Scripts>Overide_Scripts>manage_gameobject_OVERRIDE