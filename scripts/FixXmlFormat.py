import os

def fix_format():
    path = "src/Frontend/EdaSimulator.UI/Views/MainWindow.xaml"
    if not os.path.exists(path):
        print(f"Error: {path} not found")
        return
        
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()
        
    # Replace the unescaped format patterns
    old1 = "StringFormat='{0} nm Node'"
    new1 = "StringFormat='{}{0} nm Node'"
    old2 = "StringFormat='{0}-Layer Chiplet Stack'"
    new2 = "StringFormat='{}{0}-Layer Chiplet Stack'"
    
    if old1 in content:
        content = content.replace(old1, new1)
        print("Replaced BSIM node StringFormat")
    else:
        print("Warning: BSIM node StringFormat not found or already replaced")
        
    if old2 in content:
        content = content.replace(old2, new2)
        print("Replaced Chiplet stack StringFormat")
    else:
        print("Warning: Chiplet stack StringFormat not found or already replaced")
        
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
        
    print("MainWindow.xaml formats updated successfully!")

if __name__ == "__main__":
    fix_format()
