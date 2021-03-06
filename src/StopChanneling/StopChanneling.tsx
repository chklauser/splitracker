import {FunctionComponent} from "react";
import {PointsVec} from "../char";
import {useDrag} from "react-dnd";
import {ItemTypes, PointsPreviewData} from "../drag";
import {classSet} from "../ClassSet";
import {Alert} from "react-bootstrap";

export interface IStopChannelingProps {
  index: number;
  channeled: number;
}

export const StopChanneling: FunctionComponent<IStopChannelingProps> = ({index, channeled}) => {
  const points: PointsVec = {channeled: -channeled, consumed: 0, exhausted: channeled};
  const [{isDragging}, drag] = useDrag<PointsPreviewData, object, { isDragging: boolean }>(() => ({
    type: ItemTypes.PointsPreview,
    item: monitor => ({points, channelingIndex: index}),
    collect: monitor => ({
      isDragging: monitor.isDragging()
    })
  }));
  return <Alert variant={"warning"} role={"note"} ref={drag} className={classSet({StopChanneling: true, "StopChanneling-dragging": isDragging})}>
    <span className="StopChanneling-title">⚡</span>
    {
      channeled == 1
        ? <span>Einen kanalisierten Punkt in einen erschöpften Punkt umwandeln.</span>
        : <span>{channeled} kanalisierte Punkte in {channeled} erschöpfte Punkte umwandeln.</span>
    }
  </Alert>;
};
