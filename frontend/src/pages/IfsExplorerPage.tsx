import React from "react";
import SplitPaneLayout from "../components/SplitPaneLayout";
import IfsFileBrowser from "../components/IfsFileBrowser";
import IfsFileEditor from "../components/IfsFileEditor";

interface IfsExplorerPageProps {
  rootPath?: string;
}

export default function IfsExplorerPage({ rootPath = "/" }: IfsExplorerPageProps) {
  const [leftPaneWidth, setLeftPaneWidth] = React.useState(40);

  return (
    <div style={{ height: "100%", width: "100%" }}>
      <SplitPaneLayout
        left={
          <IfsFileBrowser
            startPath={rootPath}
          />
        }
        right={
          <IfsFileEditor
            filePath={undefined}
            onSave={async () => {}}
          />
        }
        leftWidth={leftPaneWidth}
        onLeftWidthChange={setLeftPaneWidth}
      />
    </div>
  );
}
