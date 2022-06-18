import {FunctionComponent} from "react";
import {IPointsBlockProps, PointsBlock} from "../PointsBlock";
import {useDrop} from "react-dnd";
import {ItemTypes, PointsPreviewData} from "../drag";
import {classSet} from "../ClassSet";
import './CurrentPoints.scss';

export interface ICurrentPointsProps extends IPointsBlockProps {
  onReceivePoints: (points: PointsPreviewData) => void;
}

export const CurrentPoints: FunctionComponent<ICurrentPointsProps> = (props) => {
  const [{isOver, canDrop}, drop] = useDrop<PointsPreviewData, object, { isOver: boolean, canDrop: boolean }>({
    accept: ItemTypes.PointsPreview,
    canDrop: (item: PointsPreviewData) => {
      let delta = item.points.consumed + item.points.exhausted + item.points.channeled;
      return delta != 0
      && delta > 0 ? props.points.consumed + props.points.exhausted + props.points.channeled < props.baseCapacity * 5
        : props.points.consumed + props.points.exhausted + props.points.channeled > 0;
    },
    drop: (incomingPoints: PointsPreviewData) => {
      props.onReceivePoints(incomingPoints);
      return undefined;
    },
    collect: monitor => ({
      isOver: monitor.isOver(),
      canDrop: monitor.canDrop()
    })
  }, [props.points, props.onReceivePoints]);
  return <div ref={drop} className={classSet({
    CurrentPoints: true
  })}>
    <PointsBlock {...props} />
    {(isOver||canDrop) && (
      <div className={classSet({"CurrentPoints-dropOverlay": true,
        "CurrentPoints-dropOverlay-over": isOver && canDrop,
        "CurrentPoints-dropOverlay-canDrop": !isOver && canDrop,
        "CurrentPoints-dropOverlay-cannotDrop": isOver && !canDrop})}>
        <p className={classSet({
          "btn": true,
          "btn-success": isOver && canDrop,
          "btn-primary": !isOver && canDrop,
          "btn-secondary": isOver && !canDrop
        })}>👉 Hierhin ziehen! 👈</p>
      </div>
    )}
  </div>;
}